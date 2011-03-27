ASMX Web Services Caching Proxy
===============================
 
Wamp, wamp. More will go here. The crux of this is that it's a simple drop-in for WSDL-generated .NET ASMX client endpoints using System.Dynamic, that caches identical (parameter/value pairs for an individual method match) calls locally. This speeds up things like: unit testing. You can keep simple TDD-like expressivity of things like:

    [Test]
    public void ListRecordsReturnsNonNull()
    {
        var records = this.service.ListRecords();
        Assert.IsNotNull(records);	
    }

and 
 
    [Test]
    public void ListRecordsReturnsAtLeastOneElement()
    {
        var records = this.service.ListRecords();
        Assert.GreaterOrEqual(1, records);	
    }

The easiest way to go about this is to simply create a `SoapHttpClientProtocol`-derived client object as you normally would, create your tests so that [if you're using an IDE] you can get the benefits o code completion, then simply change your client construction:

    // var service = new MyRemoteServiceClient();
    dynamic service = new WebServiceCachingProxy<MyRemoteServiceClient>();

and let the proxy handle caching.

Why are you running unit tests against web service endpoints, if you're even thinking of anything TDD-like? I don't know, that's your problem.
