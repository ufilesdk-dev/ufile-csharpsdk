using System;
using System.Text;

namespace UFileCSharpSDK
{
	public class Config
	{
		public static string VERSION = @"1.0.2";

		/// <summary>
		/// UCloud管理服务器地址后缀; 按bucket 的地域去对应配置
		/// 各个地域配置，参考：https://docs.ucloud.cn/ufile/tools/tools/tools_file
		/// </summary>
		public static string UCLOUD_PROXY_SUFFIX = @".cn-bj.ufileos.com";

		/// <summary>
		/// UCloud提供的公钥
		/// </summary>
        public static string UCLOUD_PUBLIC_KEY = @"paste your public key here";

		/// <summary>
		/// UCloud提供的密钥
		/// </summary>
        public static string UCLOUD_PRIVATE_KEY = @"paste your private key here";


		public static string GetUserAgent() 
		{
			return @"UCloudCSharp/" + VERSION;
		}

		public static void Init()
		{
			if (System.Configuration.ConfigurationManager.AppSettings["PUBLIC_KEY"] != null) {
				UCLOUD_PUBLIC_KEY = System.Configuration.ConfigurationManager.AppSettings["PUBLIC_KEY"];
			}
			if (System.Configuration.ConfigurationManager.AppSettings["PRIVATE_KEY"] != null) {
				UCLOUD_PRIVATE_KEY = System.Configuration.ConfigurationManager.AppSettings["PRIVATE_KEY"];
			}
			if (System.Configuration.ConfigurationManager.AppSettings["PUBLIC_KEY"] != null) {
				UCLOUD_PROXY_SUFFIX = System.Configuration.ConfigurationManager.AppSettings["PROXY_SUFFIX"];
			}
		}

	}
}

