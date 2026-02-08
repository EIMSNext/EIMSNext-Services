using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace EIMSNext.ApiCore
{
    public static class IpHelper
    {
        /// <summary>
        /// 获取客户端真实IP地址（兼容代理场景）
        /// </summary>
        /// <returns>客户端IP字符串，获取失败返回空</returns>
        public static string GetClientIp(IHttpContextAccessor httpContextAccessor)
        {
            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return string.Empty;
            }

            // 1. 优先从X-Forwarded-For请求头获取（反向代理场景）
            var forwardedForHeader = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedForHeader))
            {
                // X-Forwarded-For格式可能是：客户端IP, 代理1IP, 代理2IP
                // 取第一个非空的IP即为真实客户端IP
                var ipList = forwardedForHeader.Split(',')
                    .Select(ip => ip.Trim())
                    .Where(ip => !string.IsNullOrEmpty(ip) && IsValidIp(ip));

                if (ipList.Any())
                {
                    return ipList.First();
                }
            }

            // 2. 其次从X-Real-IP请求头获取（部分代理如Nginx常用）
            var realIpHeader = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIpHeader) && IsValidIp(realIpHeader))
            {
                return realIpHeader;
            }

            // 3. 最后获取原生的RemoteIpAddress（无代理场景）
            var remoteIp = httpContext.Connection.RemoteIpAddress;
            if (remoteIp != null)
            {
                // 处理IPv6的本地回环地址(::1)转为IPv4的127.0.0.1，方便统一处理
                if (IPAddress.IsLoopback(remoteIp) && remoteIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                {
                    return "127.0.0.1";
                }
                return remoteIp.ToString();
            }

            return string.Empty;
        }

        /// <summary>
        /// 验证IP地址格式是否合法
        /// </summary>
        private static bool IsValidIp(string ip)
        {
            return IPAddress.TryParse(ip, out _);
        }
    }
}
