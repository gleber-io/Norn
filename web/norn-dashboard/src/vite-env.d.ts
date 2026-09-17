/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Vazio (padrão) = mesma origem — só precisa em dev, apontando pra porta real do
   * `dotnet run --project src/Platform/Norn.API` (ver README). */
  readonly VITE_API_BASE_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
