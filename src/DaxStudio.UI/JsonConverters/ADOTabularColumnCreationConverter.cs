using System;
using ADOTabular.Interfaces;
using DaxStudio.UI.Model;
using Newtonsoft.Json.Converters;

namespace DaxStudio.UI.JsonConverters
{
    public class ADOTabularColumnCreationConverter: CustomCreationConverter<IADOTabularColumn>
    {
        public override IADOTabularColumn Create(Type objectType)
        {
            return new ADOTabularColumnStub();
        }
    }
}
