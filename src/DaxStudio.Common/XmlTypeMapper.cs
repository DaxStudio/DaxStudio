using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

namespace DaxStudio.Common
{



    public class XmlTypeMapper
    {
        private static readonly Hashtable xmlTypes = new Hashtable();
        private static readonly Hashtable systemTypes = new Hashtable();
        static XmlTypeMapper()
        {
            IncludeStandardTypes();
        }

        private XmlTypeMapper()
        {
        }

        public static string GetXmlType(Type type) =>
            xmlTypes[type].ToString();

        public static Type GetSystemType(string xmlType) => systemTypes[xmlType] as Type;

            private static void IncludeStandardTypes()
            {
                // Type to xmlType mapping
                xmlTypes.Add(typeof(string), "xsd:string");
                xmlTypes.Add(typeof(bool), "xsd:boolean");
                xmlTypes.Add(typeof(decimal), "xsd:decimal");
                xmlTypes.Add(typeof(float), "xsd:float");
                xmlTypes.Add(typeof(double), "xsd:double");
                xmlTypes.Add(typeof(sbyte), "xsd:byte");
                xmlTypes.Add(typeof(byte), "xsd:unsignedByte");
                xmlTypes.Add(typeof(short), "xsd:short");
                xmlTypes.Add(typeof(ushort), "xsd:unsignedShort");
                xmlTypes.Add(typeof(int), "xsd:int");
                xmlTypes.Add(typeof(uint), "xsd:unsignedInt");
                xmlTypes.Add(typeof(long), "xsd:long");
                xmlTypes.Add(typeof(ulong), "xsd:unsignedLong");
                xmlTypes.Add(typeof(DateTime), "xsd:dateTime");
                xmlTypes.Add(typeof(Guid), "uuid");
                xmlTypes.Add(typeof(byte[]), "xsd:base64Binary");
                xmlTypes.Add(typeof(DBNull), string.Empty);

                // reverse mapping of xmlType to Type
                systemTypes.Add("xsd:string",typeof(string) );
                systemTypes.Add("xsd:boolean",typeof(bool));
                systemTypes.Add("xsd:decimal",typeof(decimal));
                systemTypes.Add("xsd:float",typeof(float));
                systemTypes.Add("xsd:double",typeof(double));
                systemTypes.Add("xsd:byte", typeof(sbyte));
                systemTypes.Add("xsd:unsignedByte", typeof(byte) );
                systemTypes.Add("xsd:short", typeof(short));
                systemTypes.Add("xsd:unsignedShort", typeof(ushort));
                systemTypes.Add("xsd:int",typeof(int) );
                systemTypes.Add("xsd:unsignedInt", typeof(uint));
                systemTypes.Add("xsd:long", typeof(long));
                systemTypes.Add("xsd:unsignedLong", typeof(ulong));
                systemTypes.Add("xsd:dateTime", typeof(DateTime));
                systemTypes.Add("uuid", typeof(Guid));
                systemTypes.Add("xsd:base64Binary", typeof(byte[]));
                systemTypes.Add(string.Empty, typeof(DBNull));
            }

            public static bool IsTypeSupported(Type type) =>
                xmlTypes.ContainsKey(type);
        }
    

}
