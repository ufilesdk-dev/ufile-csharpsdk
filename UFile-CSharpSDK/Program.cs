using System;
using System.IO;
using System.Net;
using System.Threading;

namespace UFileCSharpSDK
{
	class MainClass
	{
		public static void Main (string[] args)
        {
            string bucket = "bucket name";
            string key = "key name within bucket namespace";
            string originFile = "file location to be uploaded";
            string saveFile = "file location to be saved";

            {
                //demo for putting file
                Console.WriteLine("uploading...please wait");
                Proxy.PutFileV2(bucket, key, originFile);
                Console.WriteLine(string.Format("put {0} {1} success", bucket, key));
            }


            {
                //demo for getting file
                Console.WriteLine("downloading...please wait");
                FileStream stream = new FileStream(saveFile, FileMode.CreateNew);
                Proxy.GetFile(bucket, key, stream);
                stream.Close();
                Console.WriteLine(string.Format("get {0} {1} success", bucket, key));
            }

            {
                //demo for deleting file
                Console.WriteLine("deleting...please wait");
                Proxy.DeleteFileV2(bucket, key);
                Console.WriteLine(string.Format("delete {0} {1} success", bucket, key));
            }

            {
                //demo for multi uploading file
                Console.WriteLine("uploading...please wait");
                Proxy.MultiUploader muploader = new Proxy.MultiUploader(bucket, key, originFile);
                muploader.MInit();
                muploader.MUpload(-1);
                muploader.MFinish();
                Console.WriteLine(string.Format("mupload {0} {1} success", bucket, key));
            }

            {
                //demo for multi uploading file manually
                Console.WriteLine("uploading...please wait");
                Proxy.MultiUploader muploader = new Proxy.MultiUploader(bucket, key, originFile);
                muploader.MInit();

                bool finish = false;
                for (long part = 0; part < 100000; ++part)
                {
                    if (muploader.IfLastPart()) finish = true;
                    muploader.MUpload(part);
                    if (finish) break;
                }
                muploader.MFinish();
                Console.WriteLine(string.Format("mupload {0} {1} success", bucket, key));
            }

            int sleep = 100 * 1000;
            Console.WriteLine(string.Format("finish demo, wait {0} seconds and exit...you can close this windows", sleep / 1000));
            Thread.Sleep(sleep);
        }
	}
}
