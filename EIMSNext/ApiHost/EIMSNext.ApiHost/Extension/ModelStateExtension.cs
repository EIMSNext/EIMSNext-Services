using System.Text;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace EIMSNext.ApiHost.Extension
{
    /// <summary>
    /// 
    /// </summary>
    public static class ModelStateExtension
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="modelState"></param>
        /// <returns></returns>
        public static string ToErrorString(this ModelStateDictionary modelState)
        {
            var builder = new StringBuilder();

            foreach (var err in modelState.Root.Errors)
            {
                if (!string.IsNullOrEmpty(err.ErrorMessage))
                    builder.AppendLine(err.ErrorMessage);
                if (err.Exception != null)
                    builder.AppendLine(err.Exception.Message);
            }

            if (modelState.Root.Children != null)
            {
                foreach (var child in modelState.Root.Children)
                {
                    foreach (var err in child.Errors)
                    {
                        if (!string.IsNullOrEmpty(err.ErrorMessage))
                            builder.AppendLine(err.ErrorMessage);
                        if (err.Exception != null)
                            builder.AppendLine(err.Exception.Message);
                    }
                }
            }

            return builder.ToString();
        }
    }
}
