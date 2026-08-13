# Build the shared engine + server, then run a trimmed runtime image.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only what the server needs (server references Inis.Core).
COPY Inis.Core/ Inis.Core/
COPY INISServer/ INISServer/

RUN dotnet restore INISServer/INISServer.csproj
RUN dotnet publish INISServer/INISServer.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# Serve on port 80 (reverse proxy terminates TLS upstream -> inis.nullsect0r.dev).
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80
ENTRYPOINT ["dotnet", "INISServer.dll"]
