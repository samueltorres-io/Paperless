
namespace Paperless.Configuration;

/**
 * Classe a ser herdada, que concentra a lógica de conexão cliente ao sqlite.
 * O sqlite vai rodar dentro de um container docker! Já que o SQLite é um banco em arquivo, ficará bem leve de rodar ele lá e não
 * vai gerar conflito com um possivel banco que o user esteja usando na sua máquina local!
*/