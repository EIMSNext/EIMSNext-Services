using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;

namespace EIMSNext.Service
{
    public class FormListViewService(IResolver resolver) : EntityServiceBase<FormListView>(resolver), IFormListViewService
    {
        protected override bool LogicDelete => false;
    }
}
