FROM mcr.microsoft.com/dotnet/sdk:8.0
WORKDIR /app
COPY . .
RUN dotnet publish -c Release -o /out
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
WORKDIR /out
CMD ["dotnet", "resource-planner-api.dll"]
