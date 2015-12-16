namespace Proxy.Service
{
    using System;
    using System.Runtime.CompilerServices;

    public class JsonResponse
    {
        public int Code { get; set; }

        public Proxy.Service.Data Data { get; set; }
    }
}

