using HKH.Mef2.Integration;
using EIMSNext.Core.Entity;
using EIMSNext.Core.Service;
using EIMSNext.Entity;
using EIMSNext.Service.Interface;

namespace EIMSNext.Service
{
    public class SerialNoSequenceService(IResolver resolver) : EntityServiceBase<SerialNoSequence>(resolver), ISerialNoSequenceService
    {
        private static Dictionary<SerialNoType, string> defaultSNFormats = new Dictionary<SerialNoType, string> {
            {SerialNoType.Corporate, "{0:yyyyMMdd}{1:00}{2:0000}" },
            {SerialNoType.Form,"{0:yyyyMMdd}{1:0000}" }
        };

        public string NextCorpCode(PlatformType platform)
        {
            return NextSerialNo(new NextSerialNoParameter(SerialNoType.Corporate, platform, string.Empty, string.Empty, string.Empty));
        }

        private string NextSerialNo(NextSerialNoParameter parameter)
        {
            if (parameter.SerialNoType == SerialNoType.Corporate)
            {
                var utcToday = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, 0, 0, 0, DateTimeKind.Utc);
                var currentSerialNo = Repository.Queryable.FirstOrDefault(x => x.SerialNoType == SerialNoType.Corporate);
                if (currentSerialNo == null)
                {
                    currentSerialNo = new SerialNoSequence
                    {
                        SerialNoType = SerialNoType.Corporate,
                        CurrDate = utcToday,
                        CurrId = 1
                    };
                    Repository.Insert(currentSerialNo);
                }
                else
                {
                    if (currentSerialNo.CurrDate != utcToday)
                    {
                        currentSerialNo.CurrDate = utcToday;
                        currentSerialNo.CurrId = 1;
                    }
                    else
                    {
                        currentSerialNo.CurrId += 1;
                    }
                    Repository.Replace(currentSerialNo);
                }

                var fmt = defaultSNFormats[SerialNoType.Corporate];
                return string.Format(fmt, utcToday, (int)parameter.Platform, currentSerialNo.CurrId);
            }
            else if (parameter.SerialNoType == SerialNoType.Form)
            {
                var utcToday = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, 0, 0, 0, DateTimeKind.Utc);
                var currentSerialNo = Repository.Queryable.FirstOrDefault(x => x.SerialNoType == SerialNoType.Form && x.CorpId == parameter.CorpId && x.AppId == parameter.AppId && x.FormId == parameter.FormId);
                if (currentSerialNo == null)
                {
                    currentSerialNo = new SerialNoSequence
                    {
                        SerialNoType = SerialNoType.Form,
                        CorpId = parameter.CorpId,
                        AppId = parameter.AppId,
                        FormId = parameter.FormId,
                        CurrDate = utcToday,
                        CurrId = 1
                    };
                    Repository.Insert(currentSerialNo);
                }
                else
                {
                    if (currentSerialNo.CurrDate != utcToday)
                    {
                        currentSerialNo.CurrDate = utcToday;
                        currentSerialNo.CurrId = 1;
                    }
                    else
                    {
                        currentSerialNo.CurrId += 1;
                    }
                    Repository.Replace(currentSerialNo);
                }

                var fmt = defaultSNFormats[SerialNoType.Form];
                return string.Format(fmt, utcToday, currentSerialNo.CurrId);
            }
            else
                throw new NotSupportedException("Unknown SerialNoType");
        }
    }
}
