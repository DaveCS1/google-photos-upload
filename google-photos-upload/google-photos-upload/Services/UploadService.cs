﻿using Google.Apis.PhotosLibrary.v1;
using google_photos_upload.Extensions;
using google_photos_upload.Model;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace google_photos_upload.Services
{
    public class UploadService : IUploadService
    {
        private readonly ILogger<UploadService> logger;
        private readonly IAuthenticationService authenticationService;
        private PhotosLibraryService service = null;

        public UploadService(ILogger<UploadService> logger, IAuthenticationService authenticationService)
        {
            this.logger = logger;
            this.authenticationService = authenticationService;
        }

        public bool Initialize()
        {
            service = authenticationService.GetPhotosLibraryService();

            if (service is null)
            {
                logger.LogCritical("Initialize of Google Photos API Authentication failed");
                return false;
            }

            return true;
        }

        public void ListAlbums()
        {
            MyAlbum.ListAlbums(service, logger);
        }

        public bool ProcessMainDirectory(string directorypath, bool? addifalbumexists)
        {
            var albumUploadResults = new List<Tuple<bool, string>>();
            string path = directorypath;

            if (path is null)
            {
                Console.WriteLine("# Upload Child Folders in main Folder as Albums into Google Photos");
                Console.WriteLine("What is the path to the main Folder?");
                path = Console.ReadLine();
            }

            path = path.RemoveOsPathEscapeCharacters();

            if (!Directory.Exists(path))
            {
                Console.WriteLine($"The folder '{path}' could not be found. Please try again.");
                return false;
            }

            DirectoryInfo mainDirInfo = new DirectoryInfo(path);
            foreach (var imgFolder in mainDirInfo.GetDirectories().OrderBy(di => di.Name))
            {
                var albumuploadresult = ProcessAlbumDirectoryUpload(imgFolder.FullName, addifalbumexists);

                albumUploadResults.Add(new Tuple<bool, string>(albumuploadresult.uploadResult, albumuploadresult.uploadResultText));

                if (!albumuploadresult.uploadResult)
                {
                    Console.WriteLine($"Upload failed of Album '{imgFolder.Name}'");
                }
            }

            //Print summary for user
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("------------------------");
            Console.WriteLine("Upload summary:");

            albumUploadResults.ForEach(x => Console.WriteLine(x.Item2));

            Console.WriteLine();
            Console.WriteLine();

            return true;
        }

        public bool ProcessAlbumDirectory(string directorypath, bool? addifalbumexists)
        {
            string path = directorypath;

            //If directory path is not provided from command parameter then ask user what the path is
            if (path is null)
            {
                Console.WriteLine("# Upload Folder as Album into Google Photos");
                Console.WriteLine("What folder do you want to upload?");
                path = Console.ReadLine();
            }

            path = path.RemoveOsPathEscapeCharacters();

            if (!Directory.Exists(path))
            {
                logger.LogError($"The file path could not be found: '{path}'");
                Console.WriteLine("The folder could not be found. Please try again.");
                return false;
            }


            //Process album
            var uploadResult = ProcessAlbumDirectoryUpload(path, addifalbumexists);


            //Print summary for user
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("------------------------");
            Console.WriteLine("Upload summary:");

            Console.WriteLine(uploadResult.uploadResultText);

            Console.WriteLine();
            Console.WriteLine();

            return uploadResult.uploadResult;
        }

        private (bool uploadResult, string uploadResultText) ProcessAlbumDirectoryUpload(string path, bool? addifalbumexists)
        {
            try
            {
                if (!Directory.Exists(path))
                    throw new ArgumentException($"The path '{path}' was not found.");

                DirectoryInfo dirInfo = new DirectoryInfo(path);
                string albumtitle = dirInfo.Name;

                Console.WriteLine();
                Console.WriteLine($"Uploading Album: {albumtitle}");

                MyAlbum album = new MyAlbum(logger, service, albumtitle, dirInfo);


                //Does the album already exist?
                if (!album.IsAlbumNew)
                {
                    if (!album.IsAlbumWritable)
                    {
                        return (false, "Album not updated. For safety reasons then an album created outside this utility is not updated.");
                    }
                    else
                    {
                        //Ask user if existing Album should be updated, if answer not provided through program args
                        if (addifalbumexists == null)
                        {
                            Console.Write("The album already exists, do you want to add any missing images to it? (y/n) ");

                            try
                            {
                                char key = Console.ReadKey().KeyChar;

                                if (key != 'y')
                                {
                                    Console.WriteLine();
                                    album.UploadStatus = UploadStatus.UploadAborted;
                                    return (false, album.ToStringUploadResult());
                                }
                            }
                            catch (Exception e)
                            {
                                logger.LogError(e, "An error occured when evaluating user input");
                                Console.WriteLine();
                                return (false, "An unexpected error occured, check the log");
                            }

                            Console.WriteLine();
                        }
                        else if (addifalbumexists == false)
                        {
                            logger.LogInformation("The album already exists and is not updated.");
                            return (false, album.ToStringUploadResult());
                        }
                    }
                }


                //Upload the album and images to Google Photos
                album.UploadAlbum();


                //Upload complete, share the result
                return (true, album.ToStringUploadResult());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"An exception occured during processing of '{path}'");

                return (false, $"{path}: An exception occured during Album upload");
            }
        }

    }
}
