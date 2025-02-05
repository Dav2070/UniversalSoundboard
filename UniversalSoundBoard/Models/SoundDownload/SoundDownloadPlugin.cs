﻿using Sentry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using UniversalSoundboard.Common;
using UniversalSoundboard.DataAccess;

namespace UniversalSoundboard.Models
{
    public class SoundDownloadPlugin
    {
        public string Url { get; }
        
        public SoundDownloadPlugin(string url)
        {
            Url = url;
        }

        public virtual bool IsUrlMatch()
        {
            // Regex for generic url
            Regex urlRegex = new Regex("^(https?:\\/\\/)?[\\w.-]+(\\.[\\w.-]+)+[\\w\\-._~/?#@&%\\+,;=]+");
            return urlRegex.IsMatch(Url);
        }

        public virtual async Task<SoundDownloadPluginResult> GetResult()
        {
            // Make a GET request to see if this is an audio file
            WebResponse response;

            try
            {
                var req = WebRequest.Create(Url);
                response = await req.GetResponseAsync();

                // Check if the content type is a supported audio format
                if (!Constants.allowedAudioMimeTypes.Contains(response.ContentType))
                {
                    SentrySdk.CaptureMessage("AudioFileDownload-NotSupportedFormat", scope =>
                    {
                        scope.SetTag("Link", Url);
                    });

                    throw new SoundDownloadException();
                }
            }
            catch (Exception e)
            {
                SentrySdk.CaptureException(e, scope =>
                {
                    scope.SetTag("Link", Url);
                });

                throw new SoundDownloadException();
            }

            // Get file type and file size
            string audioFileType = FileManager.FileTypeToExt(response.ContentType);
            long fileSize = response.ContentLength;

            // Try to get the file name
            Regex fileNameRegex = new Regex("^.+\\.\\w{3}$");
            string audioFileName = FileManager.loader.GetString("UntitledSound");
            string lastPart = HttpUtility.UrlDecode(Url.Split('/').Last());

            if (fileNameRegex.IsMatch(lastPart))
            {
                var parts = lastPart.Split('.');
                audioFileName = string.Join(".", parts.Take(parts.Count() - 1));
            }

            return new SoundDownloadPluginResult(new List<SoundDownloadItem>
            {
                new SoundDownloadItem(audioFileName, Url, null, Url, null, audioFileType, 0, fileSize)
            });
        }
    }
}
