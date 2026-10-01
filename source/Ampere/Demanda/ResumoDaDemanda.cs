using System.Text;
using Ampere.Core;
using Ampere.Core.Demanda;

namespace Ampere.Demanda;

/// <summary>Texto apresentado ao fim do cálculo da demanda: o total, as parcelas e onde ficou a memória.</summary>
internal static class ResumoDaDemanda
{
    public static string Texto(
        ResultadoDaDemanda resultado, PerfilDeDemanda perfil, OpcoesDaDemanda opcoes, ResultadoDoPadrao? padrao, NormaDoPadraoDeEntrada norma, string? pasta,
        IReadOnlyList<string> erros)
    {
        var texto = new StringBuilder();
        texto.AppendLine($"{perfil.Nome} · {opcoes.Edificacao}");
        texto.AppendLine();
        texto.AppendLine($"Demanda: {Kva(resultado.DemandaVA!.Value)} kVA (instalada {Kva(resultado.PotenciaInstaladaVA)} kVA)");
        foreach (var parcela in resultado.Parcelas)
            texto.AppendLine($"• {parcela.Codigo} — {parcela.Descricao}: {Kva(parcela.DemandaVA)} kVA ({parcela.Pontos} ponto(s), instalada {Kva(parcela.PotenciaInstaladaVA)} kVA)");

        if (padrao is not null)
        {
            texto.AppendLine();
            texto.AppendLine($"Padrão de entrada ({norma.Nome}{(padrao.Tabela is { } tabela ? $", {tabela.Descricao}" : string.Empty)}):");
            if (padrao.Linha is { } linha)
            {
                texto.AppendLine($"• carga instalada {Numero(padrao.CargaKw)} kW → {padrao.Fornecimento!.Nome}, disjuntor {Numero(linha.DisjuntorA)} A");
                texto.AppendLine($"• condutor do cliente {Numero(linha.CondutorFaseMm2)} ({Numero(linha.CondutorNeutroMm2)}) mm², eletroduto {linha.EletrodutoPol}\" de aço galvanizado, " +
                                 $"aterramento {Numero(linha.AterramentoMm2)} mm²");
                var ramais = new List<string>();
                if (linha.RamalCobreMultiplexadoMm2 is { } cobre) ramais.Add($"cobre multiplexado {Numero(cobre)} mm² (até 2 km da orla)");
                if (linha.RamalAluminioMm2 is { } aluminio) ramais.Add($"alumínio {linha.RamalAluminioCabo} {Numero(aluminio)} mm² (a partir de 2 km)");
                if (ramais.Count > 0) texto.AppendLine($"• ramal de conexão: {string.Join(" ou ", ramais)}");
            }
            else
            {
                foreach (var problema in padrao.Problemas) texto.AppendLine($"• não saiu: {problema}");
            }
        }

        texto.AppendLine();
        texto.AppendLine("Vigência do documento a conferir (veja a memória).");
        if (pasta is { Length: > 0 })
        {
            texto.AppendLine("Memória (JSON, Markdown e PDF):");
            texto.AppendLine(pasta);
        }

        if (erros.Count > 0)
        {
            texto.AppendLine();
            texto.AppendLine("Erros de gravação dos relatórios:");
            foreach (var erro in erros.Take(8)) texto.AppendLine($"• {erro}");
        }

        return texto.ToString();
    }

    private static string Kva(decimal va) => NumeroEmTexto.FormatarParaLeitura(va / 1000m);

    private static string Numero(decimal valor) => NumeroEmTexto.FormatarParaLeitura(valor);
}
