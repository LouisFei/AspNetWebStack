// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web.Mvc.Properties;
using System.Web.Routing;
using System.Web.SessionState;

namespace System.Web.Mvc
{
    /// <summary>
    /// 创建一个实现 IHttpHandler 接口的对象并向该对象传递请求上下文。
    /// </summary>
    public class MvcRouteHandler : IRouteHandler
    {
        private IControllerFactory _controllerFactory;

        public MvcRouteHandler()
        {
        }

        public MvcRouteHandler(IControllerFactory controllerFactory)
        {
            _controllerFactory = controllerFactory;
        }

        protected virtual IHttpHandler GetHttpHandler(RequestContext requestContext)
        {
            requestContext.HttpContext.SetSessionStateBehavior(GetSessionStateBehavior(requestContext));
            return new MvcHandler(requestContext);
        }

        protected virtual SessionStateBehavior GetSessionStateBehavior(RequestContext requestContext)
        {
            string controllerName = (string)requestContext.RouteData.Values["controller"];
            if (String.IsNullOrWhiteSpace(controllerName))
            {
                throw new InvalidOperationException(MvcResources.MvcRouteHandler_RouteValuesHasNoController);
            }

            IControllerFactory controllerFactory = _controllerFactory ?? ControllerBuilder.Current.GetControllerFactory();
            return controllerFactory.GetControllerSessionBehavior(requestContext, controllerName);
        }

        #region IRouteHandler Members

        /// <summary>
        /// 使用指定的 HTTP 上下文返回 HTTP 处理程序。
        /// </summary>
        /// <param name="requestContext"></param>
        /// <returns></returns>
        IHttpHandler IRouteHandler.GetHttpHandler(RequestContext requestContext)
        {
            return GetHttpHandler(requestContext);
        }

        #endregion
    }
}
