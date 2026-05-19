// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using RESTClient;
using System.Collections;
using System;
using System.Net;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Text;

namespace Azure.StorageServices
{
    public sealed class BlobService
    {
        private StorageServiceClient client;

        public BlobService(StorageServiceClient client)
        {
            this.client = client;
        }

        public IEnumerator CreateBlob(Action<RestResponse> callback, string resourcePath = "")
        {
            Dictionary<string, string> queryParams = new Dictionary<string, string>();
            queryParams.Add("comp", "metadata");
            
            HttpStatusCode statusCode = HttpStatusCode.Continue;
            bool isError = false;
            string code;
            StorageRequest request;
            while (true)
            {
                code = "";
                for (int i = 0; i < 12; i++)
                {
                    int rand = UnityEngine.Random.Range(0, 36);
                    if (rand < 10)
                        code += (char)('0' + rand);
                    else
                        code += (char)('a' + (rand - 10));
                }

                string path = resourcePath + "/" + code;
                request = Auth.GetAuthorizedStorageRequest(client, resourcePath, queryParams);
                yield return request.Send();
                statusCode = (HttpStatusCode)Enum.Parse(typeof(HttpStatusCode), request.Request.responseCode.ToString());
                if (statusCode == HttpStatusCode.NotFound)
                {
                    break;
                }
                else if (statusCode != HttpStatusCode.OK)
                {
                    isError = true;
                    break;
                }
            }

            RestResponse response;
            if (isError)
            {
                response = new RestResponse("Response failed with status: " + statusCode.ToString(), statusCode, request.Request.url, "");
            }
            else
            {
                response = new RestResponse(HttpStatusCode.Created, request.Request.url, code);
            }
            if (callback != null)
            {
                callback(response);
            }
        }

        /// <summary>
        /// Lists all of the containers in a storage account.
        /// </summary>
        /// <returns>The containers.</returns>
        /// <param name="">.</param>
        public IEnumerator ListContainers(Action<IRestResponse<ContainerResults>> callback)
        {
            Dictionary<string, string> queryParams = new Dictionary<string, string>();
            queryParams.Add("comp", "list");
            queryParams.Add("restype", ResType.container.ToString());
            StorageRequest request = Auth.CreateAuthorizedStorageRequest(client, Method.GET, "", queryParams);
            yield return request.Send();
            request.ParseXML<ContainerResults>(callback);
        }

        /// <summary>
        /// Lists all of the blobs in a container.
        /// </summary>
        /// <returns>The containers.</returns>
        /// <param name="">.</param>
        public IEnumerator ListBlobs(Action<IRestResponse<BlobResults>> callback, string resourcePath = "")
        {
            Dictionary<string, string> queryParams = new Dictionary<string, string>();
            queryParams.Add("comp", "list");
            queryParams.Add("restype", ResType.container.ToString());
            StorageRequest request = Auth.CreateAuthorizedStorageRequest(client, Method.GET, resourcePath, queryParams);
            yield return request.Send();
            request.ParseXML<BlobResults>(callback);
        }

        #region Download blobs

        public IEnumerator GetTextBlob(Action<RestResponse> callback, string resourcePath = "")
        {
            StorageRequest request = Auth.GetAuthorizedStorageRequest(client, resourcePath);
            yield return request.Send();
            request.GetText(callback);
        }

        public IEnumerator GetJsonBlob<T>(Action<IRestResponse<T>> callback, string resourcePath = "")
        {
            StorageRequest request = Auth.GetAuthorizedStorageRequest(client, resourcePath);
            yield return request.Send();
            request.ParseJson(callback);
        }

        public IEnumerator GetJsonArrayBlob<T>(Action<IRestResponse<T[]>> callback, string resourcePath = "")
        {
            StorageRequest request = Auth.GetAuthorizedStorageRequest(client, resourcePath);
            yield return request.Send();
            request.ParseJsonArray(callback);
        }

        public IEnumerator GetXmlBlob<T>(Action<IRestResponse<T>> callback, string resourcePath = "")
        {
            StorageRequest request = Auth.GetAuthorizedStorageRequest(client, resourcePath);
            yield return request.Send();
            request.ParseXML<T>(callback);
        }
        
        public IEnumerator GetAssetBundle(Action<IRestResponse<AssetBundle>> callback, string resourcePath = "")
        {
            StorageRequest request = Auth.GetAuthorizedStorageRequestAssetBundle(client, resourcePath);
            yield return request.Send();
            request.GetAssetBundle(callback);
        }

        public IEnumerator GetBlob(Action<IRestResponse<byte[]>, string> callback, string filename, string resourcePath = "")
        {
            string path = resourcePath + "/" + filename;
            StorageRequest request = Auth.GetAuthorizedStorageRequest(client, path);
            yield return request.Send();
            IRestResponse<byte[]> response = request.GetBytes(null);
            callback(response, filename);
        }

        public IEnumerator GetBlobData(Action<RestResponse> callback, string resourcePath = "")
        {
            Dictionary<string, string> queryParams = new Dictionary<string, string>();
            queryParams.Add("comp", "metadata");

            StorageRequest request = Auth.GetAuthorizedStorageRequest(client, resourcePath, queryParams);
            yield return request.Send();
            request.GetText(callback, false);
        }

        #endregion

        #region Upload blobs

        public IEnumerator PutTextBlob(Action<RestResponse> callback, string text, string resourcePath, string filename, string contentType = "text/plain; charset=UTF-8")
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            return PutBlob(callback, bytes, resourcePath, filename, contentType);
        }

        public IEnumerator PutImageBlob(Action<RestResponse> callback, byte[] bytes, string resourcePath, string filename, string contentType = "image/png")
        {
            return PutBlob(callback, bytes, resourcePath, filename, contentType);
        }

        public IEnumerator PutAudioBlob(Action<RestResponse> callback, byte[] bytes, string resourcePath, string filename, string contentType = "audio/wav")
        {
            return PutBlob(callback, bytes, resourcePath, filename, contentType);
        }

        public IEnumerator PutAssetBundle(Action<RestResponse> callback, byte[] bytes, string resourcePath, string filename, string contentType = "application/octet-stream")
        {
            return PutBlob(callback, bytes, resourcePath, filename, contentType);
        }

        public IEnumerator PutBlob(Action<RestResponse> callback, byte[] bytes, string resourcePath, string filename, string contentType = "application/octet-stream", string contentEncoding = null, Method method = Method.PUT)
        {
            int contentLength = bytes.Length; // TODO: check size is ok?
            Dictionary<string, string> headers = new Dictionary<string, string>();
            //string file = Path.GetFileName(filename);

            headers.Add("Content-Type", contentType);
            if (!string.IsNullOrEmpty(contentEncoding))
                headers.Add("Content-Encoding", contentEncoding);
            //headers.Add("x-ms-blob-content-disposition", string.Format("attachment; filename=\"{0}\"", filename));
            headers.Add("x-ms-blob-type", "BlockBlob");

            string filePath = resourcePath.Length > 0 ? resourcePath + "/" + filename : filename;
            StorageRequest request = Auth.CreateAuthorizedStorageRequest(client, method, filePath, null, headers, contentLength);
            request.AddBody(bytes, contentType);
            yield return request.Send();
            RestResponse response = request.Result(null);
            if (callback != null)
            {
                response.Filename = filename;
                callback(response);
            }
        }

        public IEnumerator SetBlobMetadata(Action<RestResponse> callback, string resourcePath, string filename, string user, string name, string title, string description, bool multiplayer, bool featured, string daily, Method method = Method.PUT)
        {
            Dictionary<string, string> queryParams = new Dictionary<string, string>();
            queryParams.Add("comp", "metadata");

            Dictionary<string, string> headers = new Dictionary<string, string>();
            string file = Path.GetFileName(filename);

            headers.Add("x-ms-meta-user", user);
            headers.Add("x-ms-meta-filename", name);
            headers.Add("x-ms-meta-title", title);
            if (!string.IsNullOrEmpty(description))
            {
                headers.Add("x-ms-meta-description", AzureHTML.Encode(description));
            }
            headers.Add("x-ms-meta-multiplayer", multiplayer.ToString());
            headers.Add("x-ms-meta-featured", featured.ToString());
            if (!string.IsNullOrEmpty(daily))
                headers.Add("x-ms-meta-daily", daily);

            string filePath = resourcePath.Length > 0 ? resourcePath + "/" + file : file;
            StorageRequest request = Auth.CreateAuthorizedStorageRequest(client, method, filePath, queryParams, headers);
            yield return request.Send();
            request.Result(callback);
        }

        public IEnumerator SetBlobProperties(Action<RestResponse> callback, string resourcePath, string filename, Method method = Method.PUT)
        {
            Dictionary<string, string> queryParams = new Dictionary<string, string>();
            queryParams.Add("comp", "properties");

            Dictionary<string, string> headers = new Dictionary<string, string>();
            string file = Path.GetFileName(filename);
            


            string filePath = resourcePath.Length > 0 ? resourcePath + "/" + file : file;
            StorageRequest request = Auth.CreateAuthorizedStorageRequest(client, method, filePath, queryParams, headers);
            yield return request.Send();
            request.Result(callback);
        }

        #endregion

        public IEnumerator DeleteBlob(Action<RestResponse> callback, string resourcePath, string filename)
        {
            string filePath = resourcePath.Length > 0 ? resourcePath + "/" + filename : filename;
            StorageRequest request = Auth.CreateAuthorizedStorageRequest(client, Method.DELETE, filePath);
            yield return request.Send();
            request.Result(callback);
        }
    }
}