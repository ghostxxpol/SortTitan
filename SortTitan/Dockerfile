FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY SortTitan.sln ./
COPY SortTitan.Core/SortTitan.Core.csproj SortTitan.Core/
COPY SortTitan.Generator/SortTitan.Generator.csproj SortTitan.Generator/
COPY SortTitan.Sorter/SortTitan.Sorter.csproj SortTitan.Sorter/
COPY SortTitan.Tests/SortTitan.Tests.csproj SortTitan.Tests/
RUN dotnet restore SortTitan.sln

COPY . ./
RUN dotnet publish SortTitan.Generator/SortTitan.Generator.csproj -c Release -o /out/generator --no-restore
RUN dotnet publish SortTitan.Sorter/SortTitan.Sorter.csproj -c Release -o /out/sorter --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app

COPY --from=build /out/generator ./generator/
COPY --from=build /out/sorter ./sorter/
COPY docker-entrypoint.sh ./entrypoint.sh
RUN chmod +x ./entrypoint.sh

ENTRYPOINT ["/app/entrypoint.sh"]
