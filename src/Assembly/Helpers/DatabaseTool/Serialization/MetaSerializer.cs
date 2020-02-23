using Assembly.Metro.Controls.PageTemplates.Games.Components.MetaData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Assembly.Helpers.Models
{
    class MetaSerializer
    {
        private readonly IList<MetaField> _metaStorage;

        public MetaSerializer(IList<MetaField> metaStorage)
        {
            _metaStorage = metaStorage;
        }

        public string JavaScriptMetaSerializer()
        {
            var serializer = new JavaScriptSerializer() { MaxJsonLength = 2147483644 };
            var serializedResult = serializer.Serialize(_metaStorage);

            return serializedResult;
        }
    }
}
