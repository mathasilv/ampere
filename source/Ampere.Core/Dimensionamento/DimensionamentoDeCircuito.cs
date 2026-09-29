using System.Globalization;
using Ampere.Core.Cargas;
using Ampere.Core.Memoria;
using Ampere.Core.Normas;

namespace Ampere.Core.Dimensionamento;

/// <summary>
///     Motor de dimensionamento de um circuito terminal: IB → condutores carregados → FCT → FCA → seção mínima →
///     seção pela capacidade de condução → disjuntor (IB ≤ In ≤ IZ) → queda de tensão.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>Toda referência vem do perfil (tabelas e regras) — o motor não cita norma por conta própria.</item>
///         <item>Faltou dado (tabela TODO_NORMA, linha ou temperatura não tabelada): o cálculo para, e a memória termina
///         num passo de resultado nulo explicando o que falta. Nada é aproximado.</item>
///         <item>Sem disjuntor coordenável ou com queda acima do limite, a seção sobe para a próxima nominal e a memória
///         registra o motivo.</item>
///         <item>Queda de tensão pela fórmula resistiva (sem reatância), só do circuito terminal.</item>
///         <item>Aritmética em <c>decimal</c> e memória determinística.</item>
///     </list>
/// </remarks>
public static class DimensionamentoDeCircuito
{
    private const decimal Raiz3 = 1.7320508075688772935274463415m;

    public static ResultadoDoDimensionamento Dimensionar(EntradaDeDimensionamento entrada, PerfilNormativo perfil)
    {
        var problemas = entrada.Validar();
        if (problemas.Count > 0)
        {
            return new ResultadoDoDimensionamento(entrada.Circuito, SituacaoDoDimensionamento.EntradaInvalida, perfil.Nome,
                null, null, null, null, null, null, null, null, null, problemas);
        }

        return new Calculo(entrada, perfil).Executar();
    }

    private sealed class Calculo(EntradaDeDimensionamento entrada, PerfilNormativo perfil)
    {
        private readonly List<PassoDeCalculo> _passos = [];
        private decimal? _correnteDeProjeto;
        private int? _condutoresCarregados;
        private decimal? _fct;
        private decimal? _fca;
        private decimal? _secao;
        private decimal? _capacidade;
        private decimal? _disjuntor;
        private decimal? _queda;

        public ResultadoDoDimensionamento Executar()
        {
            try
            {
                Dimensionar();
                return Resultado(SituacaoDoDimensionamento.Dimensionado, []);
            }
            catch (CalculoInterrompido interrupcao)
            {
                return Resultado(SituacaoDoDimensionamento.Interrompido, [interrupcao.Message]);
            }
        }

        private void Dimensionar()
        {
            var ib = CorrenteDeProjeto();
            var condutores = Consultar(perfil.CondutoresCarregados(entrada.Fases), "Condutores carregados",
                $"n = condutores carregados ({entrada.Fases})", [], "condutores");
            _condutoresCarregados = condutores;
            _fct = Consultar(perfil.FatorDeTemperatura(entrada.Isolacao, entrada.TemperaturaAmbienteC), "Fator de correção de temperatura",
                $"FCT = tabela ({entrada.Isolacao}; {Numero(entrada.TemperaturaAmbienteC)} °C)", [new ValorDoPasso("θ", entrada.TemperaturaAmbienteC, "°C")], string.Empty);
            _fca = Consultar(perfil.FatorDeAgrupamento(entrada.CircuitosAgrupados), "Fator de correção de agrupamento",
                $"FCA = tabela ({entrada.CircuitosAgrupados} circuitos)", [new ValorDoPasso("circuitos", entrada.CircuitosAgrupados, string.Empty)], string.Empty);

            var tipoDeCircuito = entrada.Tipo == TipoDeCarga.Iluminacao ? "Iluminacao" : "Forca";
            var secaoMinima = Consultar(perfil.SecaoMinimaMm2(tipoDeCircuito), "Seção mínima", $"Smín = tabela ({tipoDeCircuito})", [], "mm²");
            var secoes = Exigir(perfil.SecoesNominaisMm2(), "Seções nominais", "S ∈ seções nominais", "mm²");
            var candidatas = secoes.Where(secao => secao >= secaoMinima).ToList();

            var indice = SecaoPelaCapacidade(ib, candidatas, secaoMinima, secoes);
            var disjuntores = Exigir(perfil.CorrentesNominaisDeDisjuntorA(), "Correntes nominais de disjuntor", "In ∈ correntes nominais", "A");
            var resistividade = Consultar(perfil.ResistividadeOhmMm2PorM(entrada.Material), "Resistividade do condutor",
                $"ρ = tabela ({entrada.Material})", [], "Ω·mm²/m");
            var limite = Consultar(perfil.QuedaDeTensaoMaximaPct("circuito_terminal"), "Limite de queda de tensão",
                "ΔV%máx = tabela (circuito terminal)", [], "%");

            var fator = entrada.Fases is "3F" or "3F+N" ? Raiz3 : 2m;
            var elevacoes = new List<string>();
            while (true)
            {
                var secao = candidatas[indice];
                var (capacidadeDeTabela, capacidade, referenciaDaCapacidade) = Capacidade(secao);
                decimal? disjuntor = disjuntores.Where(corrente => corrente >= ib && corrente <= capacidade).Select(corrente => (decimal?)corrente).FirstOrDefault();
                var queda = fator * resistividade * entrada.ComprimentoM * ib / (secao * entrada.TensaoV) * 100m;

                var motivo = disjuntor is null
                    ? $"nenhum disjuntor entre IB = {Numero(ib)} A e IZ = {Numero(capacidade)} A"
                    : queda > limite
                        ? $"queda de tensão {Numero(Math.Round(queda, 4, MidpointRounding.AwayFromZero))}% acima do limite de {Numero(limite)}%"
                        : null;

                if (motivo is null)
                {
                    Passo(referenciaDaCapacidade, "Capacidade de condução da seção adotada", "IZ = IZ₀(S) · FCA · FCT",
                        [new ValorDoPasso("S", secao, "mm²"), new ValorDoPasso("IZ₀", capacidadeDeTabela, "A"), new ValorDoPasso("FCA", _fca!.Value, string.Empty), new ValorDoPasso("FCT", _fct!.Value, string.Empty)],
                        capacidade, "A", elevacoes.Count > 0 ? string.Join("; ", elevacoes) : null);
                    Passo(perfil.ReferenciaDaRegra(RegraNormativa.CoordenacaoCondutorProtecao), "Disjuntor", "menor In com IB ≤ In ≤ IZ",
                        [new ValorDoPasso("IB", ib, "A"), new ValorDoPasso("IZ", capacidade, "A")], disjuntor, "A",
                        $"correntes nominais: {perfil.CorrentesNominaisDeDisjuntorA().Referencia}");
                    Passo(perfil.ReferenciaDaRegra(RegraNormativa.QuedaDeTensao), "Queda de tensão", "ΔV% = k · ρ · L · IB / (S · V) · 100",
                        [new ValorDoPasso("k", fator, string.Empty), new ValorDoPasso("ρ", resistividade, "Ω·mm²/m"), new ValorDoPasso("L", entrada.ComprimentoM, "m"),
                         new ValorDoPasso("IB", ib, "A"), new ValorDoPasso("S", secao, "mm²"), new ValorDoPasso("V", entrada.TensaoV, "V")],
                        queda, "%", "fórmula resistiva (sem reatância), só o circuito terminal");

                    _secao = secao;
                    _capacidade = capacidade;
                    _disjuntor = disjuntor;
                    _queda = queda;
                    return;
                }

                if (indice + 1 >= candidatas.Count)
                    Parar(referenciaDaCapacidade, "Seção do condutor", "S = próxima seção nominal", "mm²", $"nenhuma seção do perfil atende: {motivo}");

                elevacoes.Add($"seção elevada de {Numero(secao)} para {Numero(candidatas[indice + 1])} mm²: {motivo}");
                indice++;
            }
        }

        private decimal CorrenteDeProjeto()
        {
            var referencia = perfil.ReferenciaDaRegra(RegraNormativa.CorrenteDeProjeto);
            var valores = new[] { new ValorDoPasso("S", entrada.PotenciaVA, "VA"), new ValorDoPasso("V", entrada.TensaoV, "V") };
            var (expressao, ib) = entrada.Fases switch
            {
                "F+N" or "2F" => ("IB = S / V", entrada.PotenciaVA / entrada.TensaoV),
                "3F" or "3F+N" => ("IB = S / (√3 · V)", entrada.PotenciaVA / (Raiz3 * entrada.TensaoV)),
                _ => Parar<(string, decimal)>(referencia, "Corrente de projeto", "IB = ?", "A",
                    $"configuração {entrada.Fases}: a divisão das cargas entre as fases não é modelada no MVP; use F+N ou 2F")
            };

            Passo(referencia, "Corrente de projeto", expressao, valores, ib, "A");
            _correnteDeProjeto = ib;
            return ib;
        }

        private int SecaoPelaCapacidade(decimal ib, List<decimal> candidatas, decimal secaoMinima, IReadOnlyList<decimal> secoes)
        {
            var referencia = PerfilNormativo.TodoNorma;
            for (var indice = 0; indice < candidatas.Count; indice++)
            {
                var (_, capacidade, referenciaDaTabela) = Capacidade(candidatas[indice]);
                referencia = referenciaDaTabela;
                if (capacidade < ib) continue;

                Passo(referencia, "Seção pela capacidade de condução", "menor S ≥ Smín com IZ₀(S) · FCA · FCT ≥ IB",
                    [new ValorDoPasso("Smín", secaoMinima, "mm²"), new ValorDoPasso("IB", ib, "A"), new ValorDoPasso("FCA", _fca!.Value, string.Empty), new ValorDoPasso("FCT", _fct!.Value, string.Empty)],
                    candidatas[indice], "mm²", $"seções nominais: {perfil.SecoesNominaisMm2().Referencia}");
                return indice;
            }

            var maior = candidatas.Count > 0 ? candidatas[^1] : (secoes.Count > 0 ? secoes[^1] : 0m);
            return Parar<int>(referencia, "Seção pela capacidade de condução", "menor S ≥ Smín com IZ₀(S) · FCA · FCT ≥ IB", "mm²",
                $"nenhuma seção do perfil atende IB = {Numero(ib)} A (maior seção: {Numero(maior)} mm²)");
        }

        private (decimal DeTabela, decimal Corrigida, string Referencia) Capacidade(decimal secao)
        {
            var dado = perfil.CapacidadeDeConducaoA(entrada.MetodoDeInstalacao, entrada.Isolacao, entrada.Material, _condutoresCarregados!.Value, secao);
            if (!dado.Disponivel)
            {
                Parar(dado.Referencia, "Capacidade de condução",
                    $"IZ₀ = tabela ({entrada.MetodoDeInstalacao}; {entrada.Isolacao}; {entrada.Material}; {_condutoresCarregados} condutores; {Numero(secao)} mm²)",
                    "A", dado.Ausencia!);
            }

            return (dado.Valor, dado.Valor * _fca!.Value * _fct!.Value, dado.Referencia);
        }

        private decimal Consultar(DadoNormativo<decimal> dado, string descricao, string expressao, IReadOnlyList<ValorDoPasso> valores, string unidade)
        {
            if (!dado.Disponivel) Parar(dado.Referencia, descricao, expressao, unidade, dado.Ausencia!);
            Passo(dado.Referencia, descricao, expressao, valores, dado.Valor, unidade);
            return dado.Valor;
        }

        private int Consultar(DadoNormativo<int> dado, string descricao, string expressao, IReadOnlyList<ValorDoPasso> valores, string unidade)
        {
            if (!dado.Disponivel) Parar(dado.Referencia, descricao, expressao, unidade, dado.Ausencia!);
            Passo(dado.Referencia, descricao, expressao, valores, dado.Valor, unidade);
            return dado.Valor;
        }

        private T Exigir<T>(DadoNormativo<T> dado, string descricao, string expressao, string unidade)
        {
            if (!dado.Disponivel) Parar(dado.Referencia, descricao, expressao, unidade, dado.Ausencia!);
            return dado.Valor;
        }

        private void Passo(string referencia, string descricao, string expressao, IReadOnlyList<ValorDoPasso> valores, decimal? resultado, string unidade, string? observacao = null) =>
            _passos.Add(new PassoDeCalculo(referencia, descricao, expressao, valores, resultado, unidade, observacao));

        private void Parar(string referencia, string descricao, string expressao, string unidade, string motivo) =>
            Parar<bool>(referencia, descricao, expressao, unidade, motivo);

        private T Parar<T>(string referencia, string descricao, string expressao, string unidade, string motivo)
        {
            Passo(referencia, descricao, expressao, [], null, unidade, motivo);
            throw new CalculoInterrompido(motivo);
        }

        private ResultadoDoDimensionamento Resultado(SituacaoDoDimensionamento situacao, IReadOnlyList<string> problemas) =>
            new(entrada.Circuito, situacao, perfil.Nome, _correnteDeProjeto, _condutoresCarregados, _fct, _fca, _secao, _capacidade, _disjuntor, _queda,
                new MemoriaDeCalculo(entrada.Circuito, perfil.Nome, _passos), problemas);

        private static string Numero(decimal valor) => PerfilNormativo.Numero(valor);
    }

    private sealed class CalculoInterrompido(string motivo) : Exception(motivo);
}
