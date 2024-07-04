using Client.Protos;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using System.Reflection.Metadata;
using System.Threading.Channels;
using System.Xml.Serialization;
using static Client.Protos.FileTransfer;

namespace Client
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private FileTransferClient _client;
        public FileTransferClient Client 
        {
            get
            {
                if(_client == null)
                {
                    var channel = GrpcChannel.ForAddress("https://localhost:7015");
                    _client = new FileTransferClient(channel);
                }
                return _client;
            } 
        }
        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                Console.Clear();
                await Console.Out.WriteLineAsync("==================================");
                await Console.Out.WriteLineAsync("Welcome to File Transfer");
                await Console.Out.WriteLineAsync("==================================");

                await Console.Out.WriteLineAsync("Press [1] for download\nPress [2] for upload");
                int choice = 0; 
                if(int.TryParse(Console.ReadLine(), out choice))
                {
                    switch (choice)
                    {
                        case 1:
                            await DownloadFile();
                            break;
                        case 2:
                            await UploadFile();
                            break;
                        default:
                            break;
                    }
                }
            }
        }
        private async Task UploadFile()
        {
            Console.Clear();
            await Console.Out.WriteLineAsync("Enter File Path: ");
            var filePath = await Console.In.ReadLineAsync();
            if (!Path.HasExtension(filePath))
                return;
            var fileId = Path.GetFileName(filePath);
            var fileType = Path.GetExtension(filePath);
            var request = new UploadFileRequest
            {
                Info = new Protos.FileInfo
                {
                    FileId = fileId,
                    FileType = fileType,
                }
            };
            using var stream = Client.UploadFile();
            
            await stream.RequestStream.WriteAsync(request);

            var fileBytes = await File.ReadAllBytesAsync(filePath);

            var writeTask = Task.Run(async () =>
            {
                var chunkSize = 64 * 1000; //64kb

                for (int i = 0; i < fileBytes.Length; i += chunkSize)
                {
                    var chunk = fileBytes.Skip(i).Take(chunkSize).ToArray();

                    await stream.RequestStream.WriteAsync(new UploadFileRequest
                    {
                        ChunkData = Google.Protobuf.ByteString.CopyFrom(chunk)
                    });
                }
                await stream.RequestStream.CompleteAsync();
            });

            var readTask = Task.Run(async () =>
            {
                await foreach (var response in stream.ResponseStream.ReadAllAsync())
                {
                    decimal percentage = (decimal)response.Size / (decimal)fileBytes.Length * 100;
                    Console.Clear();
                    Console.WriteLine($"Uploading {decimal.Round(percentage,2,MidpointRounding.AwayFromZero)}%");
                }
                await Console.Out.WriteLineAsync("Finished.");
                await Task.Delay(2000);
            });

            await writeTask;
            await readTask;
        }
        private async Task<GetAllItemsResponse> GetAllFiles()
        {
            var response = await Client.GetAllItemsAsync(new Empty());
            return response;
        }
        private async Task DownloadFile()
        {
            var serverFiles = await GetAllFiles();
            if (serverFiles == null || serverFiles.Items == null)
                return;
            PrintFiles(serverFiles.Items.ToList());
            await Console.Out.WriteLineAsync("============================");
            await Console.Out.WriteLineAsync("Enter file name to download: ");
            var fileName = Console.ReadLine();

            var file = serverFiles.Items.FirstOrDefault(file => file.Id == fileName);
            if (file == null)
                return;
            int totalSize = (int)file.Size;
            int tempSize = 0;

            await Console.Out.WriteLineAsync("Enter the path where you want to download the file into: ");
            var direcetoryPath = Console.ReadLine();
            if (direcetoryPath == null)
                return;
            var filePath = Path.Combine(direcetoryPath, fileName);
            File.Create(filePath).Close();

            using var call = Client.DownloadFile(new DownloadFileRequest { FileName = fileName });

            var readTask = Task.Run(async () =>
            {
                await foreach (var response in call.ResponseStream.ReadAllAsync())
                {
                    if(response.DataCase == DownloadFileResponse.DataOneofCase.Chunk)
                    {
                        using (var fs = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.None, 4096, true))
                        {
                            await fs.WriteAsync(response.Chunk.ChunkData.ToArray());
                            tempSize = (int)response.Chunk.Size;
                        }
                    }
                    else
                    {
                        await Console.Out.WriteLineAsync(response.ErrorMessage.Msg);
                        return;
                    }
                }
            });

            var progressTask = Task.Run(async () =>
            {
                while (!readTask.IsCompleted)
                {
                    decimal percentage = (decimal)tempSize/ (decimal)totalSize * 100;
                    Console.Clear();
                    await Console.Out.WriteLineAsync($"Downloading {decimal.Round(percentage, 2, MidpointRounding.AwayFromZero)}%");
                    await Task.Delay(1000);
                }
                await Console.Out.WriteLineAsync($"Downloading 100%");
                await Console.Out.WriteLineAsync("Finished");
                await Task.Delay(2000);

            });

            await readTask;
            await progressTask;
        }
        private void PrintFiles(IEnumerable<UploadFileResponse> files)
        {
            
            foreach (var file in files)
            {
                Console.Write($"file name: {Path.GetFileName(file.Id)}");
                Console.SetCursorPosition(70, Console.CursorTop);
                Console.Write($"file size: {decimal.Round((decimal)file.Size / 1024, 0, MidpointRounding.AwayFromZero)}kb");
                Console.WriteLine();
            }
                
        }
    }
}
