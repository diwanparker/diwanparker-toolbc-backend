FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY ["Toolbc.Api/Toolbc.Api.csproj", "Toolbc.Api/"]
RUN dotnet restore "Toolbc.Api/Toolbc.Api.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/Toolbc.Api"
RUN dotnet publish "Toolbc.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Toolbc.Api.dll"]
