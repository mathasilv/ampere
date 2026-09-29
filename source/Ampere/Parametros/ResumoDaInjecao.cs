using System.Text;
using Ampere.Core.Parametros;

namespace Ampere.Parametros;

/// <summary>
///     Texto apresentado ao usuário ao fim da injeção.
/// </summary>
internal static class ResumoDaInjecao
{
    public static string Texto(PlanoDeInjecao plano, TimeSpan tempo)
    {
        var texto = new StringBuilder();
        var conflitos = plano.Acoes.OfType<AcaoDeInjecao.Conflito>().ToList();
        if (conflitos.Count > 0)
        {
            texto.AppendLine($"Nada foi alterado: {conflitos.Count} conflito(s) com parâmetros já existentes no projeto.");
            texto.AppendLine();
            foreach (var conflito in conflitos) texto.AppendLine($"• {conflito.Definicao.Nome}: {conflito.Motivo}.");
            texto.AppendLine();
            texto.AppendLine("Renomeie ou remova os parâmetros indicados e rode o comando de novo.");
            return texto.ToString();
        }

        texto.AppendLine($"Criados: {plano.Acoes.OfType<AcaoDeInjecao.Criar>().Count()}");
        texto.AppendLine($"Categorias ampliadas: {plano.Acoes.OfType<AcaoDeInjecao.AmpliarCategorias>().Count()}");
        texto.AppendLine($"Já conformes: {plano.Acoes.OfType<AcaoDeInjecao.JaConforme>().Count()}");
        texto.AppendLine();
        texto.Append($"Tempo: {tempo.TotalSeconds:0.00} s");
        return texto.ToString();
    }
}
