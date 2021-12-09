using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Web;
using System.Collections.Generic;

namespace UFileCSharpSDK
{
	public class Utils
	{
		public enum RequestHeadType {
			HEAD_FIELD_CHECK
		};
		public static int bufferLen = 32 * 1024;
		public static int blockSize = 4 * 1024 * 1024;
		private static int blockSha1Size = 20;
		public static string GetMimeType(string file) {
			return MimeTypeMap.GetMimeType (Path.GetExtension (file));
		}
		public static void Copy(Stream dst, Stream src)
		{
			long l  =src.Position;
			byte[] buffer = new byte[bufferLen];
			while (true)
			{
				int n = src.Read(buffer, 0, bufferLen);
				if (n == 0) break;
				dst.Write(buffer, 0, n);
			}
			src.Seek (l, SeekOrigin.Begin);
		}
		public static long CopyNBit(Stream dst, Stream src, long numBytesToCopy)
		{
			long l  =src.Position;
			byte[] buffer = new byte[bufferLen];
			long numBytesWritten = 0;
			while (numBytesWritten < numBytesToCopy)
			{
				int len = bufferLen;
				if ((numBytesToCopy - numBytesWritten) < len)
				{
					len = (int)(numBytesToCopy - numBytesWritten);
				}
				int n = src.Read(buffer, 0, len);
				if (n == 0) break;
				dst.Write(buffer, 0, n);
				numBytesWritten += n;
			}
			src.Seek (l, SeekOrigin.Begin);
			return numBytesWritten;
		}
		public static string GetURL(string bucket, string key) 
		{
			string encodedKey = System.Web.HttpUtility.UrlEncode(key);
			encodedKey = encodedKey.Replace("+", "%20");
			return @"http://" + bucket + Config.UCLOUD_PROXY_SUFFIX + (encodedKey == string.Empty ? "" : (@"/" + encodedKey));
		}
		public static string GetMD5(string file, Int64 offset, Int64 size)
        {
            string md5String;
            FileStream fileStream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            long fileSize = fileStream.Length;
            fileStream.Seek(offset, SeekOrigin.Begin);

            System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
            if (size != 0)
            {
                if (fileSize - offset < size)
                {
                    size = fileSize - offset;
                }
                byte[] buffer = new byte[size];
                fileStream.Read(buffer, 0, (int)size);
                MemoryStream ms = new MemoryStream(buffer);
                md5String = BitConverter.ToString(md5.ComputeHash(ms)).Replace("-", String.Empty);
            }
            else
            {
                md5String = BitConverter.ToString(md5.ComputeHash(fileStream)).Replace("-", String.Empty);
            }
            fileStream.Close();
            return md5String;
        }
		public static long GetContengLength(string file)
		{
			FileInfo fileInfo = new FileInfo (file);
			return fileInfo.Length;
		}

		public static void CopyFile(HttpWebRequest request, string file) 
		{
            FileStream fileStream = File.OpenRead(file);
			Stream rs = request.GetRequestStream();
			Utils.CopyNBit (rs, fileStream, fileStream.Length);
			fileStream.Close ();
			rs.Close ();
		}
		public static void SetHeaders(HttpWebRequest request, string file, string bucket, string key, string httpVerb)
		{
			request.UserAgent = Config.GetUserAgent ();
			if (file != string.Empty) {
				request.ContentType = Utils.GetMimeType (file);
			}
			request.Method = httpVerb;
			request.Headers.Add ("Authorization", Digest.SignRequst(request, RequestHeadType.HEAD_FIELD_CHECK, bucket, key));
		}
		public static string GetSHA1(byte[] data)
		{
			SHA1 sha = new SHA1CryptoServiceProvider ();
			return System.Text.Encoding.Default.GetString (sha.ComputeHash (data));
		}
		public static byte[] GetSHA1_V2(byte[] data)
		{
			SHA1 sha = new SHA1CryptoServiceProvider();
			return sha.ComputeHash(data);
		}
		public static string GetURLSafeBase64(string data)
		{
			return HttpServerUtility.UrlTokenEncode (System.Text.Encoding.Default.GetBytes(data));
		}

		public static string CalcEtag(string filePath)
		{
			string uetag = "";

			try
			{
				using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
				{
					long fileLength = stream.Length;
					byte[] buffer = new byte[blockSize];
					byte[] finalBuffer = new byte[blockSha1Size + 4];

					uint blockCount = 1;
					if (fileLength > blockSize)
					{
						blockCount = (uint)((fileLength % blockSize == 0) ? (fileLength / blockSize) : (fileLength / blockSize + 1));
					}
					byte[] bytes = BitConverter.GetBytes(blockCount);
					if (!BitConverter.IsLittleEndian)
					{
						Array.Reverse(bytes);
					}
					Array.Copy(bytes, 0, finalBuffer, 0, bytes.Length);

					if (fileLength <= blockSize)
					{
						int readByteCount = stream.Read(buffer, 0, blockSize);
						byte[] readBuffer = new byte[readByteCount];
						Array.Copy(buffer, readBuffer, readByteCount);

						byte[] sha1Buffer = Utils.GetSHA1_V2(readBuffer);

						Array.Copy(sha1Buffer, 0, finalBuffer, 4, sha1Buffer.Length);
					}
					else
					{
						byte[] sha1AllBuffer = new byte[blockSha1Size * blockCount];

						for (int i = 0; i < blockCount; i++)
						{
							int readByteCount = stream.Read(buffer, 0, blockSize);
							byte[] readBuffer = new byte[readByteCount];
							Array.Copy(buffer, readBuffer, readByteCount);

							byte[] sha1Buffer = Utils.GetSHA1_V2(readBuffer);
							Array.Copy(sha1Buffer, 0, sha1AllBuffer, i * blockSha1Size, sha1Buffer.Length);
						}

						byte[] sha1AllBufferSha1 = Utils.GetSHA1_V2(sha1AllBuffer);

						Array.Copy(sha1AllBufferSha1, 0, finalBuffer, 4, sha1AllBufferSha1.Length);

					}
					uetag = Base64.UrlSafeBase64Encode(finalBuffer);
				}
			}
			catch (Exception e)
			{
				Console.WriteLine(e.ToString());
				throw;
			}

			return uetag;
		}

		public static string EtagByEtags(List<string> etags)
		{
			string uetag = "";

			int blockCount = etags.Count;
			if (blockCount <= 0)
			{
				throw new Exception(string.Format("etags count {0} <= 0", blockCount));
			}

			byte[] finalBuffer = new byte[blockSha1Size + 4];

			byte[] bytes = BitConverter.GetBytes(blockCount);
			if (!BitConverter.IsLittleEndian)
			{
				Array.Reverse(bytes);
			}
			Array.Copy(bytes, 0, finalBuffer, 0, bytes.Length);

			if (1 == blockCount)
			{
				byte[] sha1Buffer = Base64.UrlsafeBase64Decode(etags[0].Replace("\"", ""));
				Array.Copy(sha1Buffer, 0, finalBuffer, 4, sha1Buffer.Length);
			}
			else
			{
				byte[] sha1AllBuffer = new byte[blockSha1Size * blockCount];
				for (int i = 0; i < blockCount; i++)
				{
					byte[] sha1Buffer = Base64.UrlsafeBase64Decode(etags[i].Replace("\"", ""));
					Array.Copy(sha1Buffer, 0, sha1AllBuffer, i * blockSha1Size, sha1Buffer.Length);
				}

				byte[] sha1AllBufferSha1 = Utils.GetSHA1_V2(sha1AllBuffer);
				Array.Copy(sha1AllBufferSha1, 0, finalBuffer, 4, sha1AllBufferSha1.Length);
			}

			uetag = Base64.UrlSafeBase64Encode(finalBuffer);

			return uetag;
		}

	}
}

