FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["CourseSystem.App/CourseSystem.App.csproj", "CourseSystem.App/"]
COPY ["CourseSystem.Data/CourseSystem.Data.csproj", "CourseSystem.Data/"]
RUN dotnet restore "CourseSystem.App/CourseSystem.App.csproj"
COPY . .
WORKDIR "/src/CourseSystem.App"
RUN dotnet build "CourseSystem.App.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CourseSystem.App.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CourseSystem.App.dll"]