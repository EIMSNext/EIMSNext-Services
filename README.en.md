# EIMSNext Enterprise Information Management System

## Project Introduction

EIMSNext is an enterprise information management system designed to provide comprehensive information management solutions for enterprises. The system covers multiple modules such as authentication, data management, process control, file upload, form definition, and workflow engine. It supports data interaction through API and provides persistence support based on MongoDB.

WebSite： [www.eimsnext.com](https://www.eimsnext.com)

Demo： [work.eimsnext.com](https://work.eimsnext.com)

## Main function modules

### 1. Authentication Module (Auth)
- Provide identity authentication services based on IdentityServer.
- Supports multiple authorization methods, including password verification, CAPTCHA, single sign-on (SSO), and integrated authorization.
- Management functions of entities such as clients, resources, scopes, and users.
- Provide persistent authorization token storage and management.

### 2. API Client module (ApiClient)
- Provides a unified API client abstract class that supports RESTful API calls.
- Encapsulates common logic such as retry strategies, request building, and response handling.
- Provide a client implementation for process services, supporting operations such as approval, status query, starting a process, loading definitions, and running data flows.

### 3. Data Access Module (Core)
- Provides a data access layer based on MongoDB, supporting dynamic queries, filtering, sorting, and pagination functions.
- Includes a caching client implementation, supporting distributed caching and in-memory caching.
- Provide script engine support for executing dynamic expressions and formula calculations.

### 4. Flow Engine Module (Flow)
- A execution engine for data flow and workflow.
- Supports multiple node types, including approval, copy, insert, update, delete, query, print, etc.
- Provides a process definition parser, supporting the construction of processes from JSON configuration.
- Support the management of process-related data such as process execution logs, pending tasks, and approval records.

### 5. Service Module (Service)
- Provide a unified business service interface and implementation, supporting the addition, deletion, modification, and query operations of various entities.
- The service implementation of business entities including forms, applications, users, departments, approval processes, etc.
- Supports OData-based query interfaces, providing flexible data access capabilities.

### 6. ile Upload Module (FileUpload)
- Provide file upload service, supporting functions such as file storage, access control, and cache management.
- Includes an upload controller, supporting file upload through HTTP interface.

### 7. API Interface Module (ApiHost)
- Provide multiple API service interfaces, including authentication, file upload, process control, data services, etc.
- Supports identity authentication and permission control based on JWT.
- Provide OpenAPI/Swagger interface documentation support.

## Technical Architecture

- **Back-end technology stack**：
  - C# / .NET 8
  - MongoDB as the primary data storage
  - IdentityServer4 implements identity authentication and authorization
  - Autofac implements dependency injection
  - OData implements flexible data query interfaces
  - ClearScript V8 implements JavaScript expression parsing and execution

- **Core Components**：
  - **ApiClient**：Encapsulate REST API call logic
  - **Core**：Core Data Access and Cache Component
  - **Scripting**：Script execution engine
  - **Flow**：Process engine, supports workflow and data flow
  - **Service**：Business service layer, providing unified business logic encapsulation
  - **ApiHost**：API interface layer, providing Web API interfaces

## Instructions for Use

### 1. Start the service
- Ensure that MongoDB is installed and running.
- Configuration `appsettings.json` Database connection information.
- Start all API services (AuthApi, FileUploadApi, FlowApi, ServiceApi) /gitee.com/eimsnext/EIMSNext-Services)。

### 2. Authentication
- Use  `/auth/sendcode` Send verification code through the interface.
- Use the standard OAuth2 interface to obtain an access token.
- Supports multiple authorization methods, including password, verification code, single sign-on, integrated authorization, and more.

### 3. Data Services
- Use OData interface for data queries, supporting dynamic filtering, sorting, and pagination.
- Support performing create, read, update, and delete operations on entities such as forms, applications, users, departments, etc., through an API.

### 4. Process Service
- through `/api/v1/workflow/start` Start the process.
- through `/api/v1/workflow/approve` Submit for approval.
- through `/api/v1/dataflow/run` Trigger data flow operation.

### 5. File Upload
- Use `/api/v1/upload` Upload file through the interface.
- Support file storage path configuration, access control, and cache management.

## Development and Testing

- Use `dotnet test` Run unit tests.
- Use `MongoTransactionScope` Manage affairs.
- Use `V8ScriptEngine` Execute dynamic expression.
- Use `DynamicFindOptions` Implement flexible query logic.

### Donate
If you feel that our open source software is helpful to you, please scan the QR code below to enjoy a cup of coffee.
1. Click on the link [Paypal](https://www.paypal.com/ncp/payment/VF39PBVWQ7VVS) or scan the QR code to make a donation via Paypal

![Paypal Donation ](https://foruda.gitee.com/images/1764387152456008244/97ecd31c_12828486.png "paypal200.png")

2. Scan the QR code to make a donation via WeChat

![WeChat Donation ](https://foruda.gitee.com/images/1763474049128942966/bada62bb_12828486.jpeg "wxpay200.jpg")

## License

This project is licensed under the Apache 2.0 License. See the [LICENSE](LICENSE) file for details.

## Contribution Guidelines

Welcome to contribute code and documentation. Please follow the steps below:
1. Fork `https://gitee.com/eimsnext/EIMSNext-Services`.
2. Create a new branch.
3. Submit changes.
4. Create Pull Request。

## Contact Information

If you have any questions or suggestions, please submit an Issue or contact the project maintainer.