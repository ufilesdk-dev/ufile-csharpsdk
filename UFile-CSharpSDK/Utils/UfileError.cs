using System;
using System.IO;
using System.Runtime.Serialization.Json;

namespace UFileCSharpSDK
{
    public class UFileError
    {
        public int GetRetCode() { return RetCode; }
        public string GetErrMsg() { return ErrMsg; }
        public int RetCode;
        public string ErrMsg;
    };

    public class UFileErrorSerializer
    {
        static public string FormatString(Stream resp)
        {
            UFileError obj = (UFileError)JsonObject(resp);
            if (obj == null) {
                return "";
            }
            return string.Format("RetCode: {0} ErrMsg: {1}", obj.GetRetCode(), obj.GetErrMsg());
        }

        static public Object JsonObject(Stream resp) {
            if (resp == null) return null;

            DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(UFileError));
            UFileError obj = (UFileError)ser.ReadObject(resp);
            return obj;
        }
    }

}