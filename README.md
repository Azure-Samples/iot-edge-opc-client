# OPC Client
OPC UA client able to run OPC operations on an OPC UA server.


## Features
The client allows run single or recurring operations targeting an OPC UA server to:
- test connectivity by reading the current time node
- read node values

By default opc-client is testing the connectivity to `opc.tcp://opcplc:50000`. This can be disabled or changed to a different endpoint via command line.

## Getting Started

### Prerequisites

The implementation is based on .NET Core so it is cross-platform and recommended hosting environment is docker.

### Installation

There is no installation required.

### Quickstart

A docker container of the component is hosted in the Microsoft Container Registry and can be pulled by:

docker pull mcr.microsoft.com/iotedge/opc-client

The tags of the container match the tags of this repository and the containers are available for Windows amd64, Linux amd64 and Linux ARM32.


## Demo

The [OpcPlc](https://github.com/Azure-Samples/iot-edge-opc-plc) is an OPC UA server, which is the default target OPC UA server.

Please check out the github repository https://github.com/Azure-Samples/iot-edge-industrial-configs for sample configurations showing usage of this OPC UA client implementation.


## Resources

- [The OPC Foundation OPC UA .NET reference stack](https://github.com/OPCFoundation/UA-.NETStandard)
