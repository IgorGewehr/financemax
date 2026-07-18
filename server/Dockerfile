# financemax — Financemax.Api (F2: servidor Financeiro headless + auth e-mail+senha multi-usuário)
# Multi-stage: SDK .NET 10 para build/publish -> runtime ASP.NET 10 (menor, sem SDK).

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copia só os arquivos de projeto primeiro (cache de layer do `dotnet restore` sobrevive a
# mudanças que não mexem em dependências — só recompila do zero quando um .csproj muda).
COPY Financemax.sln ./
COPY src/SharedKernel/SistemaX.SharedKernel/SistemaX.SharedKernel.csproj src/SharedKernel/SistemaX.SharedKernel/
COPY src/Modules/Abstractions/SistemaX.Modules.Abstractions/SistemaX.Modules.Abstractions.csproj src/Modules/Abstractions/SistemaX.Modules.Abstractions/
COPY src/Infrastructure/SistemaX.Infrastructure.Local/SistemaX.Infrastructure.Local.csproj src/Infrastructure/SistemaX.Infrastructure.Local/
COPY src/Modules/Financeiro/SistemaX.Modules.Financeiro.Domain/SistemaX.Modules.Financeiro.Domain.csproj src/Modules/Financeiro/SistemaX.Modules.Financeiro.Domain/
COPY src/Modules/Financeiro/SistemaX.Modules.Financeiro.Application/SistemaX.Modules.Financeiro.Application.csproj src/Modules/Financeiro/SistemaX.Modules.Financeiro.Application/
COPY src/Modules/Financeiro/SistemaX.Modules.Financeiro.Infrastructure/SistemaX.Modules.Financeiro.Infrastructure.csproj src/Modules/Financeiro/SistemaX.Modules.Financeiro.Infrastructure/
COPY src/Modules/Identidade/SistemaX.Modules.Identidade.Domain/SistemaX.Modules.Identidade.Domain.csproj src/Modules/Identidade/SistemaX.Modules.Identidade.Domain/
COPY src/Modules/Identidade/SistemaX.Modules.Identidade.Application/SistemaX.Modules.Identidade.Application.csproj src/Modules/Identidade/SistemaX.Modules.Identidade.Application/
COPY src/Modules/Identidade/SistemaX.Modules.Identidade.Infrastructure/SistemaX.Modules.Identidade.Infrastructure.csproj src/Modules/Identidade/SistemaX.Modules.Identidade.Infrastructure/
COPY src/Financemax.Api/Financemax.Api.csproj src/Financemax.Api/
COPY tests/SistemaX.Modules.Financeiro.Tests/SistemaX.Modules.Financeiro.Tests.csproj tests/SistemaX.Modules.Financeiro.Tests/

RUN dotnet restore src/Financemax.Api/Financemax.Api.csproj

# Agora copia o resto do código e publica.
COPY src/ src/
RUN dotnet publish src/Financemax.Api/Financemax.Api.csproj \
      -c Release \
      -o /app/publish \
      --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# ICU + globalização: o alpine do .NET roda em modo INVARIANTE por padrão (sem ICU), e aí
# CultureInfo("pt-BR") — usado pra formatar dinheiro nas narrações (consultor, simulador) —
# lança CultureNotFoundException. Um financeiro brasileiro PRECISA de pt-BR. Instala o ICU e
# desliga o modo invariante. (Bug pego só no container: os testes rodam no host, que já tem ICU.)
RUN apk add --no-cache icu-libs icu-data-full

# usuário não-root — a imagem base já traz o grupo/usuário "app" (uid 64198 no alpine da MS).
RUN mkdir -p /data && chown -R app:app /data
USER app

COPY --from=build --chown=app:app /app/publish .

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV ASPNETCORE_URLS=http://+:8080
ENV Financemax__DataDir=/data
EXPOSE 8080
VOLUME ["/data"]

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD wget -qO- http://127.0.0.1:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "Financemax.Api.dll"]
