using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Services;
using System.Web.Services.Protocols;

namespace xCme.Tests
{
    public class WebServiceProxyException : Exception
    {
        public WebServiceProxyException() { }
        public WebServiceProxyException(string Message) : base(Message) { }
    }

    public class WebServiceCachingProxy<T> : DynamicObject where T : SoapHttpClientProtocol
    {
        #region Data Members

        protected T boundService;
        private Dictionary<string, WebServicesCachingMethod<T>> dictMethods;
        private Dictionary<string, FieldInfo> dictFields;
        private Dictionary<string, PropertyInfo> dictProperties;

        #endregion

        #region Properties

        public bool IsBound { get { return this.boundService != null; } }

        #endregion

        #region Constructors

        public WebServiceCachingProxy() { }

        public WebServiceCachingProxy(T service)
        {
            this.BindTo(service);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Binds to.
        /// </summary>
        /// <param name="service">The service.</param>
        public void BindTo(T service)
        {
            this.dictMethods = new Dictionary<string, WebServicesCachingMethod<T>>();
            this.dictProperties = new Dictionary<string, PropertyInfo>();
            this.dictFields = new Dictionary<string, FieldInfo>();

            var t = service.GetType();
            foreach (var method in t.GetMethods())
            {
                if (!method.Name.StartsWith("get_", StringComparison.CurrentCultureIgnoreCase)
                    && !method.Name.StartsWith("set_", StringComparison.CurrentCultureIgnoreCase)
                    && !method.Name.StartsWith("add_", StringComparison.CurrentCultureIgnoreCase)
                    && !method.Name.StartsWith("remove_", StringComparison.CurrentCultureIgnoreCase)
                    && !method.Name.EndsWith("Async", StringComparison.CurrentCultureIgnoreCase))
                {
                    dictMethods.Add(method.Name, new WebServicesCachingMethod<T>(method, service));
                }
            }

            foreach (var member in t.GetProperties())
            {
                this.dictProperties.Add(member.Name, member);
            }

            foreach (var member in t.GetFields())
            {
                this.dictFields.Add(member.Name, member);
            }

            this.boundService = service;
        }

        /// <summary>
        /// Provides the implementation for operations that get member values. Classes derived from the <see cref="T:System.Dynamic.DynamicObject"/> class can override this method to specify dynamic behavior for operations such as getting a value for a property.
        /// </summary>
        /// <param name="binder">Provides information about the object that called the dynamic operation. The binder.Name property provides the name of the member on which the dynamic operation is performed. For example, for the Console.WriteLine(sampleObject.SampleProperty) statement, where sampleObject is an instance of the class derived from the <see cref="T:System.Dynamic.DynamicObject"/> class, binder.Name returns "SampleProperty". The binder.IgnoreCase property specifies whether the member name is case-sensitive.</param>
        /// <param name="result">The result of the get operation. For example, if the method is called for a property, you can assign the property value to <paramref name="result"/>.</param>
        /// <returns>
        /// true if the operation is successful; otherwise, false. If this method returns false, the run-time binder of the language determines the behavior. (In most cases, a run-time exception is thrown.)
        /// </returns>
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            if (!this.IsBound)
            {
                throw new WebServiceProxyException("Web Service Proxy Not Bound To A Web Service. Use a constructor that provides this, or call BindTo().");
            }
            if (this.dictMethods.ContainsKey(binder.Name))
            {
                result = this.dictMethods[binder.Name];
                return true;
            }
            else if (this.dictFields.ContainsKey(binder.Name))
            {
                result = this.dictFields[binder.Name].GetValue(this.boundService);
                return true;
            }
            else if (this.dictProperties.ContainsKey(binder.Name))
            {
                result = this.dictProperties[binder.Name].GetValue(this.boundService, null);
                return true;
            }
            return base.TryGetMember(binder, out result);
        }

        /// <summary>
        /// Provides the implementation for operations that set member values. Classes derived from the <see cref="T:System.Dynamic.DynamicObject"/> class can override this method to specify dynamic behavior for operations such as setting a value for a property.
        /// </summary>
        /// <param name="binder">Provides information about the object that called the dynamic operation. The binder.Name property provides the name of the member to which the value is being assigned. For example, for the statement sampleObject.SampleProperty = "Test", where sampleObject is an instance of the class derived from the <see cref="T:System.Dynamic.DynamicObject"/> class, binder.Name returns "SampleProperty". The binder.IgnoreCase property specifies whether the member name is case-sensitive.</param>
        /// <param name="value">The value to set to the member. For example, for sampleObject.SampleProperty = "Test", where sampleObject is an instance of the class derived from the <see cref="T:System.Dynamic.DynamicObject"/> class, the <paramref name="value"/> is "Test".</param>
        /// <returns>
        /// true if the operation is successful; otherwise, false. If this method returns false, the run-time binder of the language determines the behavior. (In most cases, a language-specific run-time exception is thrown.)
        /// </returns>
        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            if (this.IsBound && binder != null && value != null)
            {
                if (this.dictFields.ContainsKey(binder.Name) && this.dictFields[binder.Name].FieldType.IsAssignableFrom(value.GetType()))
                {
                    this.dictFields[binder.Name].SetValue(this.boundService, value);
                    return true;
                }
                else if (this.dictProperties.ContainsKey(binder.Name) && this.dictProperties[binder.Name].PropertyType.IsAssignableFrom(value.GetType()))
                {
                    this.dictProperties[binder.Name].SetValue(this.boundService, value, null);
                    return true;
                }
            }
            return base.TrySetMember(binder, value);
        }

        #endregion

        private class WebServicesCachingMethod<T> : DynamicObject
        {
            #region Data Members

            private ParameterInfo[] ParameterTypes;
            private Dictionary<string, object> DictCachedResults;
            private MethodInfo Method;
            private T Service;
            private static string KeyDelimiter = "&";

            #endregion

            #region Constructors

            /// <summary>
            /// Initializes a new instance of the <see cref="WebServicesCachingMethod"/> class.
            /// </summary>
            /// <param name="dictCachedResults">The dict cached results.</param>
            public WebServicesCachingMethod(MethodInfo Method, T BoundService)
            {
                this.Method = Method;
                this.DictCachedResults = new Dictionary<string, object>();
                this.ParameterTypes = Method.GetParameters();
                this.Service = BoundService;
            }

            #endregion

            #region Public Methods

            /// <summary>
            /// Provides the implementation for operations that invoke an object. Classes derived from the <see cref="T:System.Dynamic.DynamicObject"/> class can override this method to specify dynamic behavior for operations such as invoking an object or a delegate.
            /// </summary>
            /// <param name="binder">Provides information about the invoke operation.</param>
            /// <param name="args">The arguments that are passed to the object during the invoke operation. For example, for the sampleObject(100) operation, where sampleObject is derived from the <see cref="T:System.Dynamic.DynamicObject"/> class, <paramref name="args[0]"/> is equal to 100.</param>
            /// <param name="result">The result of the object invocation.</param>
            /// <returns>
            /// true if the operation is successful; otherwise, false. If this method returns false, the run-time binder of the language determines the behavior. (In most cases, a language-specific run-time exception is thrown.
            /// </returns>
            public override bool TryInvoke(InvokeBinder binder, object[] args, out object result)
            {
                if (this.Validate(args))
                {
                    string key = Join(args, KeyDelimiter);

                    if (this.DictCachedResults.ContainsKey(key))
                    {
                        result = this.DictCachedResults[key];
                    }
                    else
                    {
                        result = this.Method.Invoke(this.Service, args);
                        this.DictCachedResults[key] = result;
                    }
                    return true;
                }
                throw new ArgumentException("Incorrect number or type of arguments to method.");
            }

            #endregion

            #region Helper Methods

            /// <summary>
            /// Joins the specified args.
            /// </summary>
            /// <param name="args">The args.</param>
            /// <param name="delimiter">The delimiter.</param>
            /// <returns></returns>
            private static string Join(object[] args, string delimiter)
            {
                StringBuilder sb = new StringBuilder();
                delimiter = delimiter == null ? string.Empty : delimiter;

                if (args != null)
                {
                    foreach (var arg in args)
                    {
                        sb.Append(arg);
                        sb.Append(delimiter);
                    }
                    if (sb.Length > delimiter.Length)
                    {
                        sb = sb.Remove(sb.Length - delimiter.Length, delimiter.Length);
                    }
                }
                return sb.ToString();
            }

            /// <summary>
            /// Validates the specified args.
            /// </summary>
            /// <param name="args">The args.</param>
            /// <param name="result">The result.</param>
            /// <returns></returns>
            private bool Validate(object[] args)
            {
                if (args == null || args.Length != this.ParameterTypes.Length)
                {
                    return false;
                }

                for (int i = 0; i < args.Length; i++)
                {
                    if (!this.ParameterTypes[i].ParameterType.IsInstanceOfType(args[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            #endregion
        }
    }    
}
