//   
//   Copyright © Microsoft Corporation, All Rights Reserved
// 
//   Licensed under the Apache License, Version 2.0 (the "License"); 
//   you may not use this file except in compliance with the License. 
//   You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0 
// 
//   THIS CODE IS PROVIDED *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
//   OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION
//   ANY IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A
//   PARTICULAR PURPOSE, MERCHANTABILITY OR NON-INFRINGEMENT.
// 
//   See the Apache License, Version 2.0 for the specific language
//   governing permissions and limitations under the License. 

namespace MessagingSamples
{
    using System;
    using Microsoft.ServiceBus;

    public class DelegatingTokenProvider : TokenProvider
    {
        TokenProvider tokenProvider;

        public DelegatingTokenProvider(TokenProvider tokenProvider)
            : base(tokenProvider.CacheTokens, tokenProvider.IsWebTokenSupported, tokenProvider.CacheSize, tokenProvider.TokenScope)
        {
            this.tokenProvider = tokenProvider;
        }

        public TokenProvider TokenProvider
        {
            get { return this.tokenProvider; }
            set
            {
                this.tokenProvider = value;
                this.CacheSize = this.tokenProvider.CacheSize;
                this.CacheTokens = this.tokenProvider.CacheTokens;
            }
        }

        protected override IAsyncResult OnBeginGetToken(string appliesTo, string action, TimeSpan timeout, AsyncCallback callback, object state)
        {
            return this.tokenProvider.BeginGetToken(appliesTo, action, false, timeout, callback, state);
        }

        protected override IAsyncResult OnBeginGetWebToken(string appliesTo, string action, TimeSpan timeout, AsyncCallback callback, object state)
        {
            return this.tokenProvider.BeginGetWebToken(appliesTo, action, false, timeout, callback, state);
        }

        protected override System.IdentityModel.Tokens.SecurityToken OnEndGetToken(IAsyncResult result, out DateTime cacheUntil)
        {
            cacheUntil = DateTime.MinValue;
            return this.tokenProvider.EndGetToken(result);
        }

        protected override string OnEndGetWebToken(IAsyncResult result, out DateTime cacheUntil)
        {
            cacheUntil = DateTime.MinValue;
            return this.tokenProvider.EndGetWebToken(result);
        }
    }
}