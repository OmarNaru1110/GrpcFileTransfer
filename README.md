# gRPC File Transfer

## Overview

This project implements a file transfer system using gRPC, providing efficient and robust methods for uploading and downloading files between a client and server. The project utilizes different types of gRPC services to handle file transfers and to keep the client informed about the progress.

## Features

- **Bidirectional Streaming for File Uploading**:
  - The client can upload a file to the server using bidirectional streaming.
  - The server sends progress updates to the client while the file is being uploaded.

- **Unary RPC and Server Streaming for File Downloading**:
  - Clients can request a list of available files on the server using unary RPC.
  - Once a file is selected, the server streams the file data back to the client using server streaming.

## Demo
<a href="https://youtu.be/x3mx3Jofzr8?si=IT-XR0irYrkK2PeZ" > file transfer using grpc (not yet) </a>
