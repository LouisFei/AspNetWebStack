// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Web.Mvc.Properties;
using System.Web.Mvc.Routing;
using System.Web.Routing;
using System.Web.WebPages;

namespace System.Web.Mvc
{
    /// <summary>
    /// 扩展 System.Web.Routing.RouteCollection 对象以进行 MVC 路由。
    /// </summary>
    public static class RouteCollectionExtensions
    {
        // This method returns a new RouteCollection containing only routes that matched a particular area.
        // The Boolean out parameter is just a flag specifying whether any registered routes were area-aware.
        private static RouteCollection FilterRouteCollectionByArea(RouteCollection routes, string areaName, out bool usingAreas)
        {
            if (areaName == null)
            {
                areaName = String.Empty;
            }

            usingAreas = false;

            // Ensure that we continue using the same settings as the previous route collection
            // if we are using areas and the route collection is exchanged
            RouteCollection filteredRoutes = new RouteCollection()
            {
                AppendTrailingSlash = routes.AppendTrailingSlash,
                LowercaseUrls = routes.LowercaseUrls,
                RouteExistingFiles = routes.RouteExistingFiles
            };

            using (routes.GetReadLock())
            {
                foreach (RouteBase route in routes)
                {
                    string thisAreaName = AreaHelpers.GetAreaName(route) ?? String.Empty;
                    usingAreas |= (thisAreaName.Length > 0);
                    if (String.Equals(thisAreaName, areaName, StringComparison.OrdinalIgnoreCase))
                    {
                        filteredRoutes.Add(route);
                    }
                }
            }

            // if areas are not in use, the filtered route collection might be incorrect
            return (usingAreas) ? filteredRoutes : routes;
        }

        /// <summary>
        /// 返回一个包含有关路由和虚拟路径的信息的对象，该路由和虚拟路径是在当前区域中生成 URL 时产生的。
        /// </summary>
        /// <param name="routes">一个包含应用程序的路由的对象。</param>
        /// <param name="requestContext">一个对象，封装有关所请求的路由的信息。</param>
        /// <param name="values">一个包含路由参数的对象。</param>
        /// <returns>一个包含有关路由和虚拟路径的信息的对象，该路由和虚拟路径是在当前区域中生成 URL 时产生的。</returns>
        public static VirtualPathData GetVirtualPathForArea(this RouteCollection routes, RequestContext requestContext, RouteValueDictionary values)
        {
            return GetVirtualPathForArea(routes, requestContext, null /* name */, values);
        }

        /// <summary>
        /// 返回一个包含有关路由和虚拟路径的信息的对象，该路由和虚拟路径是在当前区域中生成 URL 时产生的。
        /// </summary>
        /// <param name="routes">一个包含应用程序的路由的对象。</param>
        /// <param name="requestContext">一个对象，封装有关所请求的路由的信息。</param>
        /// <param name="name">要在检索 URL 路径相关信息时使用的路由的名称。</param>
        /// <param name="values">一个包含路由参数的对象。</param>
        /// <returns>一个包含有关路由和虚拟路径的信息的对象，该路由和虚拟路径是在当前区域中生成 URL 时产生的。</returns>
        public static VirtualPathData GetVirtualPathForArea(this RouteCollection routes, RequestContext requestContext, string name, RouteValueDictionary values)
        {
            bool usingAreas; // don't care about this value
            return GetVirtualPathForArea(routes, requestContext, name, values, out usingAreas);
        }

        internal static VirtualPathData GetVirtualPathForArea(this RouteCollection routes, RequestContext requestContext, string name, RouteValueDictionary values, out bool usingAreas)
        {
            if (routes == null)
            {
                throw new ArgumentNullException("routes");
            }

            if (!String.IsNullOrEmpty(name))
            {
                // the route name is a stronger qualifier than the area name, so just pipe it through
                usingAreas = false;
                return routes.GetVirtualPath(requestContext, name, values);
            }

            string targetArea = null;
            if (values != null)
            {
                object targetAreaRawValue;
                if (values.TryGetValue("area", out targetAreaRawValue))
                {
                    targetArea = targetAreaRawValue as string;
                }
                else
                {
                    // set target area to current area
                    if (requestContext != null)
                    {
                        targetArea = AreaHelpers.GetAreaName(requestContext.RouteData);
                    }
                }
            }

            // need to apply a correction to the RVD if areas are in use
            RouteValueDictionary correctedValues = values;
            RouteCollection filteredRoutes = FilterRouteCollectionByArea(routes, targetArea, out usingAreas);
            if (usingAreas)
            {
                correctedValues = new RouteValueDictionary(values);
                correctedValues.Remove("area");
            }

            VirtualPathData vpd = filteredRoutes.GetVirtualPath(requestContext, correctedValues);
            return vpd;
        }

        [SuppressMessage("Microsoft.Design", "CA1054:UriParametersShouldNotBeStrings", MessageId = "1#", Justification = "This is not a regular URL as it may contain special routing characters.")]
        public static void IgnoreRoute(this RouteCollection routes, string url)
        {
            IgnoreRoute(routes, url, null /* constraints */);
        }

        /// <summary>
        /// 忽略给定可用路由列表的指定 URL 路由。
        /// </summary>
        /// <param name="routes">应用程序的路由的集合。</param>
        /// <param name="url">要忽略的路由的 URL 模式。</param>
        /// <param name="constraints">一组表达式，用于指定 url 参数的值。</param>
        [SuppressMessage("Microsoft.Design", "CA1054:UriParametersShouldNotBeStrings", MessageId = "1#", Justification = "This is not a regular URL as it may contain special routing characters.")]
        public static void IgnoreRoute(this RouteCollection routes, string url, object constraints)
        {
            if (routes == null)
            {
                throw new ArgumentNullException("routes");
            }
            if (url == null)
            {
                throw new ArgumentNullException("url");
            }

            IgnoreRouteInternal route = new IgnoreRouteInternal(url)
            {
                Constraints = CreateRouteValueDictionaryUncached(constraints)
            };

            ConstraintValidation.Validate(route);

            routes.Add(route);
        }

        [SuppressMessage("Microsoft.Design", "CA1054:UriParametersShouldNotBeStrings", MessageId = "2#", Justification = "This is not a regular URL as it may contain special routing characters.")]
        public static Route MapRoute(this RouteCollection routes, string name, string url)
        {
            return MapRoute(routes, name, url, null /* defaults */, (object)null /* constraints */);
        }

        [SuppressMessage("Microsoft.Design", "CA1054:UriParametersShouldNotBeStrings", MessageId = "2#", Justification = "This is not a regular URL as it may contain special routing characters.")]
        public static Route MapRoute(this RouteCollection routes, string name, string url, object defaults)
        {
            return MapRoute(routes, name, url, defaults, (object)null /* constraints */);
        }

        [SuppressMessage("Microsoft.Design", "CA1054:UriParametersShouldNotBeStrings", MessageId = "2#", Justification = "This is not a regular URL as it may contain special routing characters.")]
        public static Route MapRoute(this RouteCollection routes, string name, string url, object defaults, object constraints)
        {
            return MapRoute(routes, name, url, defaults, constraints, null /* namespaces */);
        }

        [SuppressMessage("Microsoft.Design", "CA1054:UriParametersShouldNotBeStrings", MessageId = "2#", Justification = "This is not a regular URL as it may contain special routing characters.")]
        public static Route MapRoute(this RouteCollection routes, string name, string url, string[] namespaces)
        {
            return MapRoute(routes, name, url, null /* defaults */, null /* constraints */, namespaces);
        }

        [SuppressMessage("Microsoft.Design", "CA1054:UriParametersShouldNotBeStrings", MessageId = "2#", Justification = "This is not a regular URL as it may contain special routing characters.")]
        public static Route MapRoute(this RouteCollection routes, string name, string url, object defaults, string[] namespaces)
        {
            return MapRoute(routes, name, url, defaults, null /* constraints */, namespaces);
        }

        /// <summary>
        /// 映射指定的 URL 路由并设置默认的路由值、约束和命名空间。
        /// </summary>
        /// <param name="routes">应用程序的路由的集合。</param>
        /// <param name="name">要映射的路由的名称。</param>
        /// <param name="url">路由的 URL 模式。</param>
        /// <param name="defaults">一个包含默认路由值的对象。</param>
        /// <param name="constraints">一组表达式，用于指定 url 参数的值。</param>
        /// <param name="namespaces">应用程序的一组命名空间。</param>
        /// <returns>对映射路由的引用。</returns>
        [SuppressMessage("Microsoft.Design", "CA1054:UriParametersShouldNotBeStrings", MessageId = "2#", Justification = "This is not a regular URL as it may contain special routing characters.")]
        public static Route MapRoute(this RouteCollection routes, string name, string url, object defaults, object constraints, string[] namespaces)
        {
            if (routes == null)
            {
                throw new ArgumentNullException("routes");
            }
            if (url == null)
            {
                throw new ArgumentNullException("url");
            }

            Route route = new Route(url, new MvcRouteHandler())
            {
                Defaults = CreateRouteValueDictionaryUncached(defaults),
                Constraints = CreateRouteValueDictionaryUncached(constraints),
                DataTokens = new RouteValueDictionary()
            };

            ConstraintValidation.Validate(route);

            if ((namespaces != null) && (namespaces.Length > 0))
            {
                route.DataTokens[RouteDataTokenKeys.Namespaces] = namespaces;
            }

            routes.Add(name, route);

            return route;
        }

        /// <summary>
        /// The callers to this method are used at startup only, thus it's a bit better to use
        /// the uncached method because it will run faster for the first few times, and will not
        /// consume memory long term.
        /// </summary>
        private static RouteValueDictionary CreateRouteValueDictionaryUncached(object values)
        {
            var dictionary = values as IDictionary<string, object>;
            if (dictionary != null)
            {
                return new RouteValueDictionary(dictionary);
            }

            return TypeHelper.ObjectToDictionaryUncached(values);
        }

        private sealed class IgnoreRouteInternal : Route
        {
            public IgnoreRouteInternal(string url)
                : base(url, new StopRoutingHandler())
            {
            }

            public override VirtualPathData GetVirtualPath(RequestContext requestContext, RouteValueDictionary routeValues)
            {
                // Never match during route generation. This avoids the scenario where an IgnoreRoute with
                // fairly relaxed constraints ends up eagerly matching all generated URLs.
                return null;
            }
        }
    }
}
