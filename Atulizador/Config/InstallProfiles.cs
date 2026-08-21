using Atulizador.Models;

namespace Atulizador.Config;

/// <summary>
/// Dados declarativos de instalação/configuração — equivalentes a APP_LOCALIZACAO e
/// INSTALL_PROFILES no script Python. Para adicionar uma aplicação nova, basta
/// acrescentar entradas aqui; a UI (FormularioConfiguracaoIni) é genérica.
/// </summary>
public static class InstallProfiles
{
    public static readonly Dictionary<string, AppLocation> AppLocalizacao = new()
    {
        ["ServerMatriz"] = new AppLocation
        {
            NomeApp = "ServerMatriz",
            Exes = new List<string> { "ZServerMatriz.exe" },
            PalavrasChaveInstalacao = new List<string> { "servermatriz" },
        },
        ["ServerUn"] = new AppLocation
        {
            NomeApp = "ServerUn",
            Exes = new List<string> { "ServerUN.exe" },
            PalavrasChaveInstalacao = new List<string> { "serverun" },
        },
        ["NFCe"] = new AppLocation
        {
            NomeApp = "NFCe",
            Exes = new List<string> { "NFCe.exe" },
            PalavrasChaveInstalacao = new List<string> { "nfce", "nfc-e", "nfc_e" },
        },
        ["CredRP"] = new AppLocation
        {
            NomeApp = "CredRP",
            Exes = new List<string> { "CredRP.exe", "ClienrRP.exe", "Clienrrp.exe", "ClientRP.exe" },
            PalavrasChaveInstalacao = new List<string> { "credrp" },
        },
    };

    public static readonly Dictionary<string, InstallProfile> Profiles = new()
    {
        ["ServerUn"] = new InstallProfile
        {
            IniFilename = "serverun.ini",
            Fields = new List<InstallField>
            {
                new()
                {
                    Id = "host", Section = "CENTRAL", Key = "HOST",
                    Label = "IP do Server Matriz (HOST)", Tipo = FieldType.Ip,
                    Ajuda = "Use 127.0.0.1 se o ServerUN estiver na mesma máquina do Server Matriz.",
                },
                new()
                {
                    Id = "codigo_unidade", Section = "CENTRAL", Key = "CODIGO DA UNIDADE",
                    Label = "Código da Unidade", Tipo = FieldType.Numero, Largura = 3,
                },
                new()
                {
                    Id = "pdvs_validos", Section = "PDVs", Key = "PDVs VALIDOS",
                    Label = "PDVs válidos", Tipo = FieldType.PdvsValidos,
                },
                new()
                {
                    Id = "controle_atividade_utiliza", Section = "CONTROLE ATIVIDADE", Key = "UTILIZA",
                    Label = "Utilizar Controle de Atividade?", Tipo = FieldType.SimNao,
                },
                new()
                {
                    Id = "controle_atividade_conexao", Section = "CONTROLE ATIVIDADE", Key = "CONEXAO",
                    Label = "IP de conexão do Controle de Atividade", Tipo = FieldType.MesmoQue,
                    Referencia = "host", PerguntaMesmo = "Usar o mesmo IP do HOST (Server Matriz)?",
                },
                new()
                {
                    Id = "banco_wrpdv_conexao", Section = "BANCO WRPDV", Key = "CONEXAO",
                    Label = "IP do Banco de Dados (WRPDV)", Tipo = FieldType.Ip, TestarPorta = 5432,
                },
            },
        },
        ["ServerMatriz"] = new InstallProfile
        {
            IniFilename = "Server.ini",
            Fields = new List<InstallField>
            {
                new()
                {
                    Id = "ip_local", Section = "RPDV WL", Key = "IP Local",
                    Label = "IP Local desta máquina (Server Matriz)", Tipo = FieldType.IpAuto,
                    Ajuda = "Detectado automaticamente pela rede desta máquina. Ajuste manualmente " +
                             "se houver mais de uma placa de rede.",
                },
                new()
                {
                    Id = "conexao_banco", Section = "RPDV WL", Key = "Conexao",
                    Label = "IP do Banco de Dados (Postgres)", Tipo = FieldType.Ip, TestarPorta = 5432,
                },
                new()
                {
                    Id = "flexdb_conexao", Section = "FlexDB", Key = "Conexao",
                    Label = "Conexão do FlexDB (ERP)", Tipo = FieldType.Espelho, Referencia = "conexao_banco",
                },
                new()
                {
                    Id = "controle_atividade_conexao", Section = "CONTROLE ATIVIDADE", Key = "CONEXAO",
                    Label = "Conexão do Controle de Atividade", Tipo = FieldType.Espelho, Referencia = "ip_local",
                },
            },
        },
        ["CredRP"] = new InstallProfile
        {
            // Atenção: o arquivo se chama "server.ini" igual ao do ServerMatriz, mas fica
            // numa pasta diferente (C:\wrpdv\CredRP) — não há conflito.
            IniFilename = "server.ini",
            Fields = new List<InstallField>
            {
                new()
                {
                    Id = "conexao_erp", Section = "SERVIDOR", Key = "Conexao",
                    Label = "Conexão do banco ERP", Tipo = FieldType.Espelho,
                    OutroApp = "ServerMatriz", Referencia = "flexdb_conexao",
                },
                new()
                {
                    Id = "conexao_wrpdv", Section = "WRPDV", Key = "Conexao",
                    Label = "Conexão do banco WRPDV", Tipo = FieldType.Espelho,
                    OutroApp = "ServerMatriz", Referencia = "conexao_banco",
                },
                new()
                {
                    Id = "lojas", Section = "LOJAS", Label = "Lojas (Nome + CNPJ)", Tipo = FieldType.ListaLojas,
                    PrefixoChave = "LJ", CampoNome = "Nome", CampoCnpj = "CNPJ",
                },
            },
        },
        ["NFCe"] = new InstallProfile
        {
            IniFilename = "ConfigNFe.ini",
            Fields = new List<InstallField>
            {
                new()
                {
                    Id = "codigo_unidade", Section = "Geral", Key = "Unidade",
                    Label = "Código da Unidade", Tipo = FieldType.Numero, Largura = 3,
                },
                new()
                {
                    Id = "banco_endereco", Section = "Banco de Dados", Key = "Endereco",
                    Label = "IP do Banco de Dados", Tipo = FieldType.MesmoQueOutroApp, TestarPorta = 5432,
                    PerguntaMesmo = "Instalado junto com o Server Matriz (mesma máquina)? Se sim, uso o mesmo " +
                                     "IP de banco já configurado lá.",
                    OutroApp = "ServerMatriz", Referencia = "conexao_banco",
                },
                new()
                {
                    Id = "controle_atividade_utiliza", Section = "Controle de Atividade", Key = "Utiliza",
                    Label = "Utilizar Controle de Atividade?", Tipo = FieldType.SimNao,
                },
                new()
                {
                    Id = "controle_atividade_ip", Section = "Controle de Atividade", Key = "IP",
                    Label = "IP do Controle de Atividade", Tipo = FieldType.Ip,
                },
            },
        },
    };

    /// <summary>
    /// Confere se todo campo Espelho/MesmoQue/MesmoQueOutroApp aponta para um id que
    /// realmente existe (no mesmo perfil ou no perfil de outro app referenciado).
    /// Equivalente a validar_install_profiles() no Python.
    /// </summary>
    public static List<string> Validar()
    {
        var erros = new List<string>();

        foreach (var (nomePerfil, perfil) in Profiles)
        {
            var idsDoPerfil = perfil.Fields.Select(c => c.Id).ToHashSet();
            foreach (var campo in perfil.Fields)
            {
                if (campo.Tipo is not (FieldType.Espelho or FieldType.MesmoQue or FieldType.MesmoQueOutroApp))
                    continue;

                var referencia = campo.Referencia;
                if (string.IsNullOrEmpty(referencia))
                {
                    erros.Add($"{nomePerfil}.{campo.Id}: tipo \"{campo.Tipo}\" sem \"Referencia\" definida.");
                    continue;
                }

                var outroApp = campo.OutroApp;
                if (!string.IsNullOrEmpty(outroApp))
                {
                    if (!Profiles.TryGetValue(outroApp, out var perfilOutro))
                    {
                        erros.Add($"{nomePerfil}.{campo.Id}: OutroApp \"{outroApp}\" não existe em Profiles.");
                        continue;
                    }
                    var idsDoOutro = perfilOutro.Fields.Select(c => c.Id).ToHashSet();
                    if (!idsDoOutro.Contains(referencia))
                    {
                        erros.Add($"{nomePerfil}.{campo.Id}: Referencia \"{referencia}\" não existe no perfil \"{outroApp}\".");
                    }
                }
                else if (!idsDoPerfil.Contains(referencia))
                {
                    erros.Add($"{nomePerfil}.{campo.Id}: Referencia \"{referencia}\" não existe no próprio perfil \"{nomePerfil}\".");
                }
            }
        }

        return erros;
    }
}
