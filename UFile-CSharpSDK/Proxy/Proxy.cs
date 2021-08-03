using System;
using System.Net;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Runtime.Serialization.Json;
using System.Threading;

namespace UFileCSharpSDK
{

	public class Proxy
	{
		public static void GetFile(string bucket, string key, Stream stream)
		{
            HttpWebRequest request = null;
            HttpWebResponse response = null;
            try
            {
                request = (HttpWebRequest)WebRequest.Create(Utils.GetURL(bucket, key));
                request.KeepAlive = false;
                Utils.SetHeaders(request, string.Empty, bucket, key, "GET");

                response = HttpWebResponseExt.GetResponseNoException(request);
                Stream body = response.GetResponseStream();
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    string e = UFileErrorSerializer.FormatString(body);
                    throw new Exception(string.Format("{0} {1}", response.StatusDescription, e));
                }

                int bytesRead;
                byte[] buffer = new byte[1024];
                while ((bytesRead = body.Read(buffer, 0, buffer.Length)) != 0)
                    stream.Write(buffer, 0, bytesRead);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                throw;
            }
            finally {
                if (request != null) request.Abort();
                if (response != null) response.Close();
            }
			return;
		}

        [Obsolete("use method DeleteFileV2")]
		public static string DeleteFile(string bucket, string key)
		{
			string strResult = string.Empty;
            HttpWebRequest request = null;
            HttpWebResponse response = null;
            try
            {
                request = (HttpWebRequest)WebRequest.Create(Utils.GetURL(bucket, key));
                request.KeepAlive = false;
                Utils.SetHeaders(request, string.Empty, bucket, key, "DELETE");

                response = HttpWebResponseExt.GetResponseNoException(request);
                Stream body = response.GetResponseStream();
                if (response.StatusCode != HttpStatusCode.NoContent)
                {
                    string e = UFileErrorSerializer.FormatString(body);
                    throw new Exception(string.Format("{0} {1}", response.StatusDescription, e));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                throw;
            }
            finally
            {
                if (request != null) request.Abort();
                if (response != null) response.Close();
            }
			return strResult;
		}

        public static void DeleteFileV2(string bucket, string key) {
            DeleteFile(bucket, key);
        }

        [Obsolete("use method PutFileV2")]
		public static string PutFile(string bucket, string key, string file) 
		{
			if (!System.IO.File.Exists(file)) {
				throw new Exception(string.Format("{0} does not exist", file));
			}
			string strResult = string.Empty;
            HttpWebRequest request = null;
            HttpWebResponse response = null;
            try
            {
                string url = Utils.GetURL(bucket, key);
                request = (HttpWebRequest)WebRequest.Create(url);
                request.KeepAlive = false;
                Utils.SetHeaders(request, file, bucket, key, "PUT");
                Utils.CopyFile(request, file);

                response = HttpWebResponseExt.GetResponseNoException(request);
                Stream body = response.GetResponseStream();
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    string e = UFileErrorSerializer.FormatString(body);
                    throw new Exception(string.Format("{0} {1}", response.StatusDescription, e));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                throw;
            }
            finally
            {
                if (request != null) request.Abort();
                if (response != null) response.Close();
            }
			return strResult;
		}

        public static void PutFileV2(string bucket, string key, string file) {
            PutFile(bucket, key, file);
        }

        public class MInitResponse {

            public void Parse(Stream stream) {

                if (null == stream) {
                    throw new Exception("null stream to call MInitResponse.Parse");
                }

                try
                {
                    DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(MInitResponse));
                    MInitResponse obj = (MInitResponse)ser.ReadObject(stream);
                    
                    this.UploadId = obj.GetUploadId();
                    this.BlkSize = obj.GetBlkSize();
                }catch(Exception e){
                    Console.WriteLine(e.ToString());
                    throw;
                }
            }

            public string GetUploadId() { return UploadId; }
            public long GetBlkSize() { return BlkSize; }
            //must be public because json deserialization
            public string UploadId;
            public long BlkSize;
        };

        public class MUploadResponse {
            public void Parse(Stream stream)
            {

                if (null == stream)
                {
                    throw new Exception("null stream to call MInitResponse.Parse");
                }

                try
                {
                    DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(MInitResponse));
                    MUploadResponse obj = (MUploadResponse)ser.ReadObject(stream);
                    this.PartNumber = obj.GetPartNumber();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    throw;
                }
            }

            public long GetPartNumber() { return PartNumber; }

            public long PartNumber;
        };

        //NOTE: concurrently upload is NOT supported
        public class MultiUploader {

            public enum PROCESS_TYPE{
                MINIT = 0,
                MUPLOAD,
                MFINISH,
                MCANCEL
            };

            public MultiUploader(string bucket, string key, string file) {
                m_bucket = bucket;
                m_key = key;
                m_filename = file;
                m_part_number = 0;
                m_etags = new List<string>();
                try
                {
                    m_file = File.OpenRead(file);
                }catch(Exception e){
                    Console.WriteLine(string.Format("open file {0} fail:{1}", file, e.ToString()));
                    throw;
                }
            }

            ~MultiUploader() {
                m_file.Close();
            }
            public void MInit() {

                HttpWebRequest request = null;
                HttpWebResponse response = null;
                try
                {
                    string url = URL(PROCESS_TYPE.MINIT);
                    request = (HttpWebRequest)WebRequest.Create(url);
                    request.Method = "POST";
                    request.KeepAlive = false;
                    Utils.SetHeaders(request, "", m_bucket, m_key, "POST");
                    response = HttpWebResponseExt.GetResponseNoException(request);
                    Stream body = response.GetResponseStream();

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        string e = UFileErrorSerializer.FormatString(body);
                        throw new Exception(string.Format("{0} {1}", response.StatusDescription, e));
                    }

                    //parsing to get the uploadid & blksize
                    MInitResponse minitRes = new MInitResponse();
                    minitRes.Parse(body);

                    m_uploadid = minitRes.GetUploadId();
                    m_blk_size = minitRes.GetBlkSize();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    throw;
                }
                finally
                {
                    if (request != null) request.Abort();
                    if (response != null) response.Close();
                }
            }

            //part == -1 means the caller wants to upload all the parts one after after another
            //otherwise,MUpload will only upload one indicated part(for example,you need to upload
            //the same part when this part upload failed before)
            public void MUpload(long part = -1) {

                long total_parts_upload = -1;
                if (part == -1)
                {
                    m_part_number = 0;
                }
                else {
                    m_part_number = part;
                    total_parts_upload = 1;
                }

                HttpWebRequest request = null;
                HttpWebResponse response = null;
                try
                {

                    long parts_uploaded = 0;
                    bool finished = false;
                    int retry_count = 3, i = 0;
                    while (true)
                    {
                        if (finished || (total_parts_upload != -1 && parts_uploaded >= total_parts_upload)) break;

                        string url = URL(PROCESS_TYPE.MUPLOAD);
                        request = (HttpWebRequest)WebRequest.Create(url);
                        request.Method = "PUT";
                        request.KeepAlive = false;

                        Utils.SetHeaders(request, m_filename, m_bucket, m_key, "PUT");
                        m_file.Seek(m_part_number * m_blk_size, 0);

                        MemoryStream ms = new MemoryStream();
                        long n = Utils.CopyNBit(ms, m_file, m_blk_size);
                        if (n < m_blk_size)
                        {
                            finished = true;
                            if (n == 0) break;
                        }
                        ms.Position = 0;
                        //set content-length
                        request.ContentLength = n;
                        Utils.CopyNBit(request.GetRequestStream(), ms, n);

                        response = HttpWebResponseExt.GetResponseNoException(request);
                        Stream body = response.GetResponseStream();

                        if (response.StatusCode != HttpStatusCode.OK)
                        {
                            if (i < retry_count - 1)
                            {
                                finished = false;
                                response.Close();
                                i += 1;
                                Thread.Sleep(1000);
                                continue;
                            }
                            else
                            {
                                finished = true;
                                string e = UFileErrorSerializer.FormatString(body);
                                throw new Exception(string.Format("{0} {1}", response.StatusDescription, e));
                            }
                        }
                        else
                        {
                            string etag = response.GetResponseHeader("ETag");
                            m_etags.Add(etag);
                        }
                        response.Close();

                        m_part_number += 1;
                        parts_uploaded += 1;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    throw;
                }
                finally
                {
                    if (request != null) request.Abort();
                    if (response != null) response.Close();
                }
            }

            public void MFinish() {

                HttpWebRequest request = null;
                HttpWebResponse response = null;
                try
                {
                    string url = URL(PROCESS_TYPE.MFINISH);
                    request = (HttpWebRequest)WebRequest.Create(url);
                    request.Method = "POST";
                    request.KeepAlive = false;
                    Utils.SetHeaders(request, "", m_bucket, m_key, "POST");

                    MemoryStream ms = new MemoryStream();
                    StreamWriter sw = new StreamWriter(ms);
                    for (int idx = 0; idx < m_etags.Count; ++idx)
                    {
                        if (idx != m_etags.Count - 1) {
                            sw.Write(m_etags[idx] + ",");
                        }
                        else{
                            sw.Write(m_etags[idx]);
                        }
                    }

                    sw.Flush();
                    ms.Position = 0;
                    Utils.Copy(request.GetRequestStream(), ms);

                    response = HttpWebResponseExt.GetResponseNoException(request);
                    Stream body = response.GetResponseStream();
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        string e = UFileErrorSerializer.FormatString(body);
                        throw new Exception(string.Format("{0} {1}", response.StatusDescription, e));
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    throw;
                }
                finally {
                    if (request != null) request.Abort();
                    if (response != null) response.Close();
                }
            }

            public bool IfLastPart() {
                FileInfo fi = new FileInfo(m_filename);
                long filesize = fi.Length;
                long last_part = filesize / m_blk_size;
                if (filesize % m_blk_size == 0) --last_part;
                return last_part == m_part_number;
            }

            private string URL(PROCESS_TYPE type) {
                string encodedKey = System.Web.HttpUtility.UrlEncode(m_key);
                encodedKey = encodedKey.Replace("+", "%20");
                switch (type) {
                    case PROCESS_TYPE.MINIT:
                        return string.Format("http://{0}{1}/{2}?uploads", m_bucket, Config.UCLOUD_PROXY_SUFFIX, encodedKey);
                    case PROCESS_TYPE.MUPLOAD:
                        return string.Format("http://{0}{1}/{2}?uploadId={3}&partNumber={4}", m_bucket, Config.UCLOUD_PROXY_SUFFIX, encodedKey, m_uploadid, m_part_number);
                    case PROCESS_TYPE.MFINISH:
                        return string.Format("http://{0}{1}/{2}?uploadId={3}", m_bucket, Config.UCLOUD_PROXY_SUFFIX, encodedKey, m_uploadid);
                    case PROCESS_TYPE.MCANCEL: return "";
                        return string.Format("http://{0}{1}/{2}?uploadId={3}", m_bucket, Config.UCLOUD_PROXY_SUFFIX, encodedKey, m_uploadid);
                    default:
                        throw new Exception("invalid url type for multiuploader");
                }
            }

            private string m_bucket;
            private string m_key;
            private string m_filename;
            private string m_uploadid;
            private long m_blk_size;
            private long m_part_number;
            private FileStream m_file;
            private List<string> m_etags;
        };
	}
}