﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using rarLib;

namespace nntpPoster
{
    public class UsenetPoster
    {
        private UsenetPosterConfig configuration;
        public UsenetPoster(UsenetPosterConfig configuration)
        {
            this.configuration = configuration;
        }

        public void PostFileToUsenet(FileInfo file)
        {
            nntpMessagePoster poster = new nntpMessagePoster(configuration);
            List<FileToPost> filesToPost = PrepareFileToPost(configuration, file);
            List<PostedFileInfo> postedFiles = new List<PostedFileInfo>();

            Int32 fileCount = 1;
            foreach (var fileToPost in filesToPost)
            {
                String comment1 = String.Format("{0}/{1}", fileCount++, filesToPost.Count);
                PostedFileInfo postInfo = fileToPost.PostYEncFile(poster, comment1, "");
                postedFiles.Add(postInfo);
            }
            
            poster.WaitTillCompletion();
            XDocument nzbDoc = GenerateNzbFromPostInfo(file.Name, postedFiles);
            nzbDoc.Save(file.Name + ".nzb");
        }

        private List<FileToPost> PrepareFileToPost(UsenetPosterConfig configuration, FileInfo file)
        {
            DirectoryInfo fileWorkingfolder = new DirectoryInfo(
                Path.Combine(configuration.WorkingFolder.FullName, file.NameWithoutExtension()));
            if (!fileWorkingfolder.Exists)
                fileWorkingfolder.Create();
            
            //TODO: if we obscufate it will be at this point
            file.CopyTo(Path.Combine(fileWorkingfolder.FullName, file.Name)); //Copy the file into a folder with the filename.
            DirectoryInfo compressedFiles = MakeRarFiles(fileWorkingfolder, file.NameWithoutExtension());
            return compressedFiles.GetFiles().Select(f => new FileToPost(configuration, f)).ToList();
        }

        private DirectoryInfo MakeRarFiles(DirectoryInfo workingFolder, String fileNameWithoutExtension)
        {
            Int64 directorySize = workingFolder.Size();
            var rarSizeRecommendation = configuration.RarFileSizeMap
                .Where(rr => rr.FromFileSize < directorySize)
                .OrderByDescending(rr => rr.FromFileSize)
                .First();
            DirectoryInfo targetDirectory = new DirectoryInfo(Path.Combine(
                workingFolder.Parent.FullName, fileNameWithoutExtension + "_proc"));
            targetDirectory.Create();
            var rarWrapper = new RarWrapper(configuration.RarToolLocation);
            rarWrapper.CompressDirectory(
                workingFolder, targetDirectory, fileNameWithoutExtension, rarSizeRecommendation.ReccomendedRarSize);

            return targetDirectory;
        }

        private XDocument GenerateNzbFromPostInfo(String title, List<PostedFileInfo> postedFiles)
        {
            XNamespace ns = "http://www.newzbin.com/DTD/2003/nzb";

            XDocument doc = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XDocumentType("nzb", "-//newzBin//DTD NZB 1.1//EN", "http://www.newzbin.com/DTD/nzb/nzb-1.1.dtd", null),
                new XElement(ns + "nzb",
                    new XElement(ns + "head",
                        new XElement(ns + "meta",
                            new XAttribute("type", "title"),
                            title
                            )
                        ),
                    postedFiles.Select(f =>
                        new XElement(ns + "file",
                            new XAttribute("poster", configuration.FromAddress),
                            new XAttribute("date", f.PostedDateTime.GetUnixTimeStamp()),
                            new XAttribute("subject", f.NzbSubjectName),
                            new XElement(ns + "groups",
                                f.PostedGroups.Select(g => new XElement(ns + "group", g))
                                ),
                            new XElement(ns + "segments",
                                f.Segments.OrderBy(s => s.SegmentNumber).Select(s =>
                                    new XElement(ns + "segment",
                                        new XAttribute("bytes", s.Bytes),
                                        new XAttribute("number", s.SegmentNumber),
                                        s.MessageIdWithoutBrackets
                                        )
                                    )
                                )
                            )
                        )
                    )
                );

            return doc;
        }
    }
}