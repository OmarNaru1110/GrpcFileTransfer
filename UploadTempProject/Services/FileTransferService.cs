using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Server.Protos;
using System.IO;
using static Server.Protos.FileTransfer;

namespace Server.Services
{
    public class FileTransferService:FileTransferBase
    {
        public override async Task UploadFile(IAsyncStreamReader<UploadFileRequest> requestStream, IServerStreamWriter<UploadFileResponse> responseStream, ServerCallContext context)
        {
            var fileId = string.Empty;
            var filePath = string.Empty;
            var fileType = string.Empty;
            int totalSize = 0;
            FileStream fs = null;

            var readTask = Task.Run(async () =>
            {
                try
                {
                    while (await requestStream.MoveNext())
                    {
                        var request = requestStream.Current;

                        if (request.DataCase == UploadFileRequest.DataOneofCase.ChunkData)
                        {
                            var chunkBytes = request.ChunkData.ToByteArray();
                            if (fs!=null)
                            {
                                await fs.WriteAsync(chunkBytes, 0, chunkBytes.Length);
                                totalSize += chunkBytes.Length;
                            }
                        }
                        else if (request.DataCase == UploadFileRequest.DataOneofCase.Info)
                        {
                            fileId = request.Info.FileId.TrimEnd(Path.GetExtension(request.Info.FileId).ToArray());//remove extension
                            fileId += Guid.NewGuid().ToString("N").Substring(0,15) + Path.GetExtension(request.Info.FileId);

                            filePath = Path.Combine("D:\\Computer Science\\Courses\\Backend\\11. gRPC\\UploadTempProject\\UploadTempProject\\UploadedFiles", $"{fileId}");
                            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                            fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                        }
                    }
                }
                finally
                {
                    if(fs != null)
                    {
                        await fs.FlushAsync();
                        fs.Close();
                        fs.Dispose();
                    }
                }

            });
            
            //**
            var writeTask = Task.Run(async () =>
            {
                while (!readTask.IsCompleted)
                {
                    await responseStream.WriteAsync(new UploadFileResponse
                    {
                        Id = fileId,
                        Size = (uint)totalSize
                    });
                    await Task.Delay(1000); // Update progress every second
                }
            });
            //**

            await readTask;
            await responseStream.WriteAsync(new UploadFileResponse
            {
                Id = fileId,
                Size = (uint)totalSize
            });
            
            //writeTask.Dispose();
        }
        public override Task<GetAllItemsResponse> GetAllItems(Empty request, ServerCallContext context)
        {
            var directoryPath = "D:\\Computer Science\\Courses\\Backend\\11. gRPC\\UploadTempProject\\UploadTempProject\\UploadedFiles";
            var files = Directory.GetFiles(directoryPath)
                        .Select(file => new UploadFileResponse { Id = Path.GetFileName(file), Size = (uint) new System.IO.FileInfo(file).Length});

            var response = new GetAllItemsResponse();
            response.Items.AddRange(files);
            return Task.FromResult(response);
        }
        public override async Task DownloadFile(DownloadFileRequest request, IServerStreamWriter<DownloadFileResponse> responseStream, ServerCallContext context)
        {
            var directoryPath = "D:\\Computer Science\\Courses\\Backend\\11. gRPC\\UploadTempProject\\UploadTempProject\\UploadedFiles";

            var filePath = Directory.GetFiles(directoryPath).FirstOrDefault(filePath => Path.GetFileName(filePath) == request.FileName);

            if(filePath == null)
                await responseStream.WriteAsync(new DownloadFileResponse { ErrorMessage = new Error {Msg = "file not found" } });

            byte[] fileBytes = await File.ReadAllBytesAsync(filePath);

            int chunkSize = 64 * 1024; //64kb

            for(int i = 0; i < fileBytes.Length; i += chunkSize)
            {
                var chunk = fileBytes.Skip(i).Take(chunkSize).ToArray();

                await responseStream.WriteAsync(new DownloadFileResponse { Chunk = new DownloadFileChunks { ChunkData = ByteString.CopyFrom(chunk), Size = (uint)(i + chunkSize) } });
            }
        }
    }
}
