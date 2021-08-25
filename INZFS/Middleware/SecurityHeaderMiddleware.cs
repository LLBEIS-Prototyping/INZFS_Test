﻿using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace INZFS.Web.Middleware
{
    public class SecurityHeaderMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeaderMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public Task Invoke(HttpContext context)
        {
            context.Response.Headers.Add("referrer-policy", new StringValues("strict-origin-when-cross-origin"));
           
            context.Response.Headers.Add("x-content-type-options", new StringValues("nosniff"));
   
            context.Response.Headers.Add("x-frame-options", new StringValues("DENY"));

            context.Response.Headers.Add("X-Permitted-Cross-Domain-Policies", new StringValues("none"));

            context.Response.Headers.Add("x-xss-protection", new StringValues("1; mode=block"));

            context.Response.Headers.Add("frame-ancestors", new StringValues("none"));

            context.Response.Headers.Add("Content-Security-Policy", new StringValues(
            "base-uri 'self';" +
            "font-src 'self' https://www.gov.uk/assets/static/fonts/;" +
       
            "form-action 'self'" +
            "frame-ancestors 'none';" +
            "frame-src 'none';" +
            "img-src 'self' http://www.w3.org/2000/svg;" +
           
            "media-src 'self';" +
            "object-src 'self';" +
            "script-src 'self' https://ajax.aspnetcdn.com/ajax/jquery/jquery-3.6.0.min.js https://code.jquery.com/jquery-3.6.0.js https://design-system.service.gov.uk/javascripts/govuk-frontend-d7b7e40c8ac2bc81d184bb2e92d680b9.js ;" 
            
            ));;

            return _next(context);
        }
    }

}
