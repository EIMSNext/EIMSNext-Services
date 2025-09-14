using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EIMSNext.Auth.Entity
{
    public static class CustomGrantType
    {
        public const string VerificationCode = "verification_code";
        public const string SingleSignOn = "sso_credentials";
        public const string Integration = "integration";
    }
}
