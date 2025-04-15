# Backend Battle Challenge

## Task Overview
- **Develop a Backend Service:**  
  - Handle player registration, leaderboard management, and battle requests.
  - Implement a battle engine to simulate and manage battles programmatically.
- **Technology & Database:**  
  - Flexibility in language and framework with a recommendation to use Redis or a familiar DB.

## Backend Service Requirements
- **Endpoints/Handlers:**
  - **Create Player:** Validate inputs and store player data.
  - **Submit Battle:** Enable battle initiation and queue the request.
  - **Retrieve Leaderboard:** Return a ranked list of players including rank, score, and identifier.

## Battle Processor and Engine Requirements
- **Battle Processing:**
  - Process battles in submission order.
  - Ensure single processing per battle with immediate, sequential processing.
  - Update player resources: Winner steals 5–10% of each resource type from the loser.

## Additional Considerations
- **Code Quality:** Focus on clean, maintainable code with thoughtful design decisions and clear documentation.
- **Documentation:** Provide setup instructions, key design decisions, tests, and note any assumptions or trade-offs.
- **Concurrency & Security:**  
  - Support simultaneous battle processing (non-overlapping players).
  - Protect endpoints from unauthorized access.

## Solution
*Battle* is a comprehensive solution to the *Backend Development Hands-on Test* challenge. The project implements a backend service with a battle engine to handle player registration, leaderboard management, and battle processing, all built according to the challenge specifications.

## Technology Stack
- **C# / ASP.NET Core**  
  Chosen as the primary framework because me personal familiarity with it, robust ecosystem, ease of use, and proven top-tier performance.

- **gRPC**  
  Offers several performance and design benefits that align well with the project's goals, especially when dealing with real-time processing in gaming industry.
  High-performance, strongly typed contracts, bi-directional streaming...

  The **Battle.API** server has `gRPC Reflection` enabled for testing purposes. Just be aware it's a security vurnerability having this enabled in production.

- **Redis**  
  Utilized as the main data store per the challenge guidelines. The project incorporates:
  - **RedisOM:** An object mapper that simplifies interactions with Redis.
  - **Redis PubSub & Redis Streams:** Employed to manage inter-process communications and notifications between the API and the background services.

- **Keycloak**  
  Integrated as a production-ready open-source identity provider to ensure robust security across all API endpoint, a critical aspect often overlooked.

- **Battle.Cli**  
  Console app that uses the complete architecture. It could be use as an alternative or a complement of tools like gRPCurl.

- **Docker Compose:**  
  Managing and orchestrating multiple containers into one reproducible environment for seamless development. 
  Two yamls are available; the "production-like", and the one ideal for development, which not include the Battle.API as a service.

## Project Architecture & Design Decisions
- **Decoupled (logically) API and Background Processing:**  
  - The **API Layer** handles player registration, battle submissions, and leaderboard retrieval.
  - **Background Services:**  
    - **Battle Processing:** Handles the execution of battle logic, including attack calculations, defense checks and the generation of detailed battle result.
    - **Report Generation:** Runs in parallel to create comprehensive battle reports, manage the resources, updating Players Stats and Dashboards data.
  - Both background services operate asynchronously, leveraging Redis Streams and PubSub for notifications and data synchronization, ensuring the main API remains responsive.
  - It could have been spplited in more that 2 "Services", but I took this decision to keep it simple. 

- **Additional Internal Notes:**  
  - I decided to centralize the authentication flow. The system uses OAuth 2.0, but it uses the main Battle.API to get the auth code URL and the access token. This way the client is completely agnostic to the identity provider used. It also gives us the ability to monitor and audit each token generated in an easy way. This decision has some advantages and disadvantages.
  - Due to time constraints, several areas in the project lacks of tests for integration and end-to-end functionality. While these types of tests are important, they're also time consuming.
  - Due to time constraints, again, in-depth code refactoring was limited.
  - While deploying across multiple services would be ideal for a production setup, this project intentionally keeps deployment simple. The use of background services for report creation and battle processing was chosen to decouple (logically) the main API from resource-intensive tasks.

## Future Enhancements
- **Code Refactoring:**  
  With additional time, further refactoring would enhance the clarity and maintainability of the codebase.
  
- **Multi-Service Deployment:**  
  A more segmented deployment strategy could be adopted to separate concerns even further and improve scalability.
  
- **Extended Reporting and Error Handling:**  
  Adding granular battle reporting and more robust error handling mechanisms are planned for future iterations.

- **Improve Resilience:**  
  Libraries like `Polly` could be used to implement Retries policies, Rate-limit endpoints, and overall, to make the project more resilient.

## Setup & Running Instructions
- **Setup:**  
  - Docker
  - .NET 9 SDK
  - Postman, gRPCurl, gRPCui... Any tool capable of test gRPC endpoints.

- **Configuration:**
  - A valid SSL certificate is required for running gRPC in HTTP/2. To do that:

```
# In Windows isn't neccesary but Mac OS fails otherwise
1. mkdir ${HOME}/.aspnet/https/     

# Replace password with your desired password. 
# You can skip the steps 2 and 3 if you already have a valid HTTPS developer certificate.
2. dev-certs https -ep ${HOME}/.aspnet/https/aspnetapp.pfx -p <<password>>  
3. dotnet dev-certs https --trust
4. Set your password in the file docker-compose.yaml, in the <<password>> placeholder.
```

- **Run Production-like:**
```
  - docker compose up -d
  - (for the Cli) dotnet run --configuration Release --launch-profile production
```

- **Run Development:**
```
  - docker-compose -f docker-compose.development.yaml -d
  - (for the Cli) dotnet run --configuration Release --launch-profile development
```

- **Urls Production-like:**
  - Battle.API: https://localhost:48081 
  - Auth URL: https://localhost:48081/api/v1/authentication/url
  - Access Token Exchange URL: https://localhost:48081/api/v1/authentication/token
  - Redis: 127.0.0.1:47379
  - Redis insight WEB: http://localhost:47001
  - Keycloak: http://localhost:49090

- **Urls Development:**
  - Battle.API: https://localhost:7006 
  - Auth URL: https://localhost:7006/api/v1/authentication/url
  - Access Token Exchange URL: https://localhost:7006/api/v1/authentication/token
  - Redis: 127.0.0.1:47379
  - Redis insight WEB: http://localhost:47001
  - Keycloak: http://localhost:49090

- **Both cases:**
  - You could use Visual Studio, Rider, VS or your favourite IDE to run the CLI and the API. 

- **Troubleshooting:**
  - Detailed guidelines for installing and configuring the ASP.NET Core HTTPS certificate are provided [here](https://learn.microsoft.com/en-us/aspnet/core/security/docker-compose-https?view=aspnetcore-9.0).
  - If `dotnet run` is used to run the API and the CLI, do not forget doing it in the appropriate folder.