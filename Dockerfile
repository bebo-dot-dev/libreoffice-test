FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app
    
# Copy csproj and restore as distinct layers
COPY libreoffice-test.csproj ./
RUN dotnet restore
    
# Copy everything else and build
COPY . .
RUN dotnet publish -c Release -o out
    
# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0-jammy
WORKDIR /app
COPY --from=build-env /app/out .

RUN apt-get update -y && apt-get install libreoffice -y
RUN apt-get install wget -y && wget https://bootstrap.pypa.io/get-pip.py && python3 get-pip.py
RUN pip install unoserver
ENV ASPNETCORE_ENVIRONMENT="Docker"

ENTRYPOINT ["dotnet", "libreoffice-test.dll"]