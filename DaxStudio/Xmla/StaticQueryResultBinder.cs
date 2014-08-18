using DaxStudio.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.Controllers;
using System.Web.Http.ModelBinding;
using System.Web.Http.ValueProviders;

namespace DaxStudio.ExcelAddin.Xmla
{
    public class StaticQueryResultBinder:IModelBinder
    {
        public bool BindModel(HttpActionContext actionContext, ModelBindingContext bindingContext)
        {
            if (bindingContext.ModelType != typeof(IStaticQueryResult))
            {
                return false;
            }
            var rdr = new Newtonsoft.Json.JsonTextReader(new StringReader(actionContext.Request.Content.ReadAsStringAsync().Result));
            
            var json = Newtonsoft.Json.JsonSerializer.Create(new Newtonsoft.Json.JsonSerializerSettings() );
            
            var res = json.Deserialize<StaticQueryResult>(rdr);
            bindingContext.Model = res;
            /*
            ValueProviderResult val = bindingContext.ValueProvider.GetValue(
                bindingContext.ModelName);
            if (val == null)
            {
                return false;
            }

            string key = val.RawValue as string;
            if (key == null)
            {
                bindingContext.ModelState.AddModelError(
                    bindingContext.ModelName, "Wrong value type");
                return false;
            }
            */
            /*
            IStaticQueryResult result;
            if (IStaticQueryResult.TryGetValue(key, out result) || IStaticQueryResult.TryParse(key, out result))
            {
                bindingContext.Model = result;
                return true;
            }
            */
            bindingContext.ModelState.AddModelError(
                bindingContext.ModelName, "Cannot convert value to Location");
            return false;
        }
    }
}
