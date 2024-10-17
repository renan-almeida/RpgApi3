using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RpgApi.Data;
using RpgApi.Models;

namespace RpgApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DisputasController : ControllerBase
    {
        private readonly DataContext _context;

        public DisputasController(DataContext context)
        {
            _context = context;
        }

        [HttpPost("Arma")]
        public async Task<IActionResult> AtaqueComArmaAsync(Disputa d)
        {
            try
            {
                //Programação dos próximos aqui
                Personagem? atacante = await _context.TB_PERSONAGENS
                .Include(p => p.Arma)
                .FirstOrDefaultAsync(p => p.Id == d.AtacanteId);

                Personagem? oponente = await _context.TB_PERSONAGENS
                .FirstOrDefaultAsync(p => p.Id == d.OponenteId);

                int dano = atacante.Arma.Dano + new Random().Next(atacante.Forca);
                dano = dano - new Random().Next(oponente.Defesa);

                if (dano > 0)
                    oponente.PontosVida = oponente.PontosVida - (int)dano;
                if (oponente.PontosVida <= 0)
                    d.Narracao = $"{oponente.Nome} foi derrotado!";

                _context.TB_PERSONAGENS.Update(oponente);
                await _context.SaveChangesAsync();

                StringBuilder dados = new StringBuilder();
                dados.AppendFormat(" Atacamte: {0}. ", atacante.Nome);
                dados.AppendFormat(" Oponente {0}. ", oponente.Nome);
                dados.AppendFormat(" Pontos de vida do atacante: {0}.", atacante.PontosVida);
                dados.AppendFormat(" Pontos de vida do oponente: {0}", oponente.PontosVida);
                dados.AppendFormat(" Arma utilizada: {0}", atacante.Arma.Nome);
                dados.AppendFormat(" Dano: {0}", dano);

                d.Narracao += dados.ToString();
                d.Datadisputa = DateTime.Now;
                _context.TB_DISPUTAS.Add(d);
                _context.SaveChanges();


                return Ok(d);
            }
            catch (System.Exception ex)
            {

                return BadRequest(ex.Message);
            }
        }


        [HttpPost("DisputaEmGrupo")]

        public async Task<IActionResult> DisputaEmGrupoAsync(Disputa d)
        {
            try
            {
                d.Resultados = new List<string>(); //Instancia a lista de resultados

                //Busca na base dos personagens informados no parametro incluindo armas e habilidades.
                List<Personagem> personagems = await _context.TB_PERSONAGENS
                .Include(p => p.Arma)
                .Include(p => p.PersonagemHabilidades).ThenInclude(ph => ph.Habilidade)
                .Where(p => d.ListaIdPersonagens.Contains(p.Id)).ToListAsync();

                int qtdPersonagensVivos = personagems.FindAll(p => p.PontosVida > 0).Count;

                while (qtdPersonagensVivos > 1)
                {
                    List<Personagem> atacantes = personagems.Where(p => p.PontosVida > 0).ToList();
                    Personagem atacante = atacantes[new Random().Next(atacantes.Count)];
                    d.AtacanteId = atacante.Id;

                    List<Personagem> oponentes = personagems.Where(p => p.Id != atacante.Id && p.PontosVida > 0).ToList();
                    Personagem oponente = oponentes[new Random().Next(oponentes.Count)];
                    d.OponenteId = oponente.Id;

                    int dano = 0;
                    string ataqueUsado = string.Empty;
                    string resultado = string.Empty;

                    bool ataqueUsaArma = (new Random().Next(1) == 0);

                    if (ataqueUsaArma && atacante.Arma != null)
                    {
                        dano = atacante.Arma.Dano + (new Random().Next(atacante.Forca));
                        dano = dano - new Random().Next(oponente.Defesa);
                        ataqueUsado = atacante.Arma.Nome;

                        if (dano > 0)
                            oponente.PontosVida = oponente.PontosVida - (int)dano;

                        resultado =
                                string.Format("{0} atacou {1} usando {2} com o dano {3}.", atacante.Nome + oponente.Nome + ataqueUsado + dano);
                        d.Narracao += resultado;
                        d.Resultados.Add(resultado);
                    }
                    else if (atacante.PersonagemHabilidades.Count != 0)
                    {
                        int sorteioHabilidadeId = new Random().Next(atacante.PersonagemHabilidades.Count);
                        Habilidade habilidadeEscolhida = atacante.PersonagemHabilidades[sorteioHabilidadeId].Habilidade;
                        ataqueUsado = habilidadeEscolhida.Nome;

                        dano = habilidadeEscolhida.Dano + (new Random().Next(atacante.Inteligencia));
                        dano = dano - new Random().Next(oponente.Defesa);

                        if (dano > 0)
                            oponente.PontosVida = oponente.PontosVida - (int)dano;

                        resultado =
                             string.Format("{0} atacou {1} usando {2} com o dano {3}.", atacante.Nome + oponente.Nome + ataqueUsado + dano);
                        d.Narracao += resultado;
                        d.Resultados.Add(resultado);

                    }
                    //Atenção: Aqui ficará a Programação da verificação do ataque usado e verificação se existe mais de um personagem vivo

                    if (!string.IsNullOrEmpty(ataqueUsado))
                    {
                        atacante.Vitorias++;
                        oponente.Derrotas++;
                        atacante.Disputas++;
                        oponente.Disputas++;

                        d.Id = 0; //Zera o id para poder salvar os dados de disputa sem erro de chave.
                        d.Datadisputa = DateTime.Now;
                        _context.TB_DISPUTAS.Add(d);
                        await _context.SaveChangesAsync();

                    }

                    qtdPersonagensVivos = personagems.FindAll(p => p.PontosVida > 0).Count;

                    if (qtdPersonagensVivos == 1) // havendo só um personagem vivo, exite um campeão!
                    {
                        string resultadoFinal =
                        $"{atacante.Nome.ToUpper()} é CAMPEÃO com {atacante.PontosVida} pontos de vida restantes!";
                        d.Narracao += resultadoFinal;
                        d.Resultados.Add(resultadoFinal);

                        break;
                    }
                    _context.TB_PERSONAGENS.UpdateRange(personagems);
                    await _context.SaveChangesAsync();

                }

                return Ok(d);
            }



            catch (System.Exception ex)
            {
                return BadRequest(ex.Message);
            }



        }

        [HttpDelete("ApagarDisputas")]
        public async Task<IActionResult> DeleteAsync()
        {
            try
            {
                List<Disputa> disputas = await _context.TB_DISPUTAS.ToListAsync();
                _context.TB_DISPUTAS.RemoveRange(disputas);
                await _context.SaveChangesAsync();
                return Ok("Disputas apagadas");
            }
            catch (System.Exception ex)
            { return BadRequest(ex.Message); }
        }

        [HttpGet("Listar")]
        public async Task<IActionResult> ListarAsync()
        {
            try
            {
                List<Disputa> disputas =
                await _context.TB_DISPUTAS.ToListAsync();
                return Ok(disputas);
            }
            catch (System.Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        [HttpPut("RestaurarPontosVida")]
        public async Task<IActionResult> RestaurarPontosVidaAsync(Personagem p)
        {
            try
            {
                int linhasAfetadas = 0;
                Personagem? pEncontrado =
                await _context.TB_PERSONAGENS.FirstOrDefaultAsync(pBusca => pBusca.Id == p.Id);
                pEncontrado.PontosVida = 100;
                bool atualizou = await TryUpdateModelAsync<Personagem>(pEncontrado, "p",
                pAtualizar => pAtualizar.PontosVida);
                // EF vai detectar e atualizar apenas as colunas que foram alteradas.
                if (atualizou)
                    linhasAfetadas = await _context.SaveChangesAsync();
                return Ok(linhasAfetadas);
            }
            catch (System.Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }


        //Método para alteração da foto
        [HttpPut("AtualizarFoto")]
        public async Task<IActionResult> AtualizarFotoAsync(Personagem p)
        {
            try
            {
                Personagem personagem = await _context.TB_PERSONAGENS
                .FirstOrDefaultAsync(x => x.Id == p.Id);
                personagem.FotoPersonagem = p.FotoPersonagem;
                var attach = _context.Attach(personagem);
                attach.Property(x => x.Id).IsModified = false;
                attach.Property(x => x.FotoPersonagem).IsModified = true;
                int linhasAfetadas = await _context.SaveChangesAsync();
                return Ok(linhasAfetadas);
            }
            catch (System.Exception ex)
            { return BadRequest(ex.Message); }
        }

        [HttpPut("ZerarRanking")]
        public async Task<IActionResult> ZerarRankingAsync(Personagem p)
        {
            try
            {
                Personagem pEncontrado =
                await _context.TB_PERSONAGENS.FirstOrDefaultAsync(pBusca => pBusca.Id == p.Id);
                pEncontrado.Disputas = 0;
                pEncontrado.Vitorias = 0;
                pEncontrado.Derrotas = 0;
                int linhasAfetadas = 0;
                bool atualizou = await TryUpdateModelAsync<Personagem>(pEncontrado, "p",
                pAtualizar => pAtualizar.Disputas,
                pAtualizar => pAtualizar.Vitorias,
                pAtualizar => pAtualizar.Derrotas);
                // EF vai detectar e atualizar apenas as colunas que foram alteradas.
                if (atualizou)
                    linhasAfetadas = await _context.SaveChangesAsync();
                return Ok(linhasAfetadas);
            }
            catch (System.Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("ZerarRankingRestaurarVidas")]
        public async Task<IActionResult> ZerarRankingRestaurarVidasAsync()
        {
            try
            {
                List<Personagem> lista =
                await _context.TB_PERSONAGENS.ToListAsync();
                foreach (Personagem p in lista)
                {
                    await ZerarRankingAsync(p);
                    await RestaurarPontosVidaAsync(p);
                }
                return Ok();
            }
            catch (System.Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }


        [HttpGet("GetByPerfil/{userId}")]
        public async Task<IActionResult> GetByPerfilAsync(int userId)
        {
            try
            {
                Usuario usuario = await _context.TB_USUARIOS
                .FirstOrDefaultAsync(x => x.Id == userId);
                List<Personagem> lista = new List<Personagem>();
                if (usuario.Perfil == "Admin")
                    lista = await _context.TB_PERSONAGENS.ToListAsync();
                else
                    lista = await _context.TB_PERSONAGENS
                    .Where(p => p.Usuario.Id == userId).ToListAsync();
                return Ok(lista);
            }
            catch (System.Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("GetByNomeAproximado/{nomePersonagem}")]
        public async Task<IActionResult> GetByNomeAproximado(string nomePersonagem)
        {
            try
            {
                List<Personagem> lista = await _context.TB_PERSONAGENS
                .Where(p => p.Nome.ToLower().Contains(nomePersonagem.ToLower()))
                .ToListAsync();
                return Ok(lista);
            }
            catch (System.Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{usuarioId}")]
        public async Task<IActionResult> GetUsuario(int usuarioId)
        {
            try
            {
                //List exigirá o using System.Collections.Generic
                Usuario usuario = await _context.TB_USUARIOS //Busca o usuário no banco através do Id
                .FirstOrDefaultAsync(x => x.Id == usuarioId);
                return Ok(usuario);
            }
            catch (System.Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }


        [HttpGet("GetByLogin/{login}")]
        public async Task<IActionResult> GetUsuario(string login)
        {
            try
            {
                //List exigirá o using System.Collections.Generic
                Usuario usuario = await _context.TB_USUARIOS //Busca o usuário no banco através do login
                .FirstOrDefaultAsync(x => x.Username.ToLower() == login.ToLower());
                return Ok(usuario);
            }
            catch (System.Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        //Método para alteração da geolocalização
        [HttpPut("AtualizarLocalizacao")]
        public async Task<IActionResult> AtualizarLocalizacao(Usuario u)
        {
            try
            {
                Usuario usuario = await _context.TB_USUARIOS //Busca o usuário no banco através do Id
                .FirstOrDefaultAsync(x => x.Id == u.Id);
                usuario.Latitude = u.Latitude;
                usuario.Longitude = u.Longitude;
                var attach = _context.Attach(usuario);
                attach.Property(x => x.Id).IsModified = false;
                attach.Property(x => x.Latitude).IsModified = true;
                attach.Property(x => x.Longitude).IsModified = true;
                int linhasAfetadas = await _context.SaveChangesAsync(); //Confirma a alteração no banco
                return Ok(linhasAfetadas); //Retorna as linhas afetadas (Geralmente sempre 1 linha msm)
            }
            catch (System.Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }


        [HttpPut("AtualizarEmail")]
        public async Task<IActionResult> AtualizarEmail(Usuario u)
        {
            try
            {
                Usuario usuario = await _context.TB_USUARIOS //Busca o usuário no banco através do Id
                .FirstOrDefaultAsync(x => x.Id == u.Id);
                usuario.Email = u.Email;
                var attach = _context.Attach(usuario);
                attach.Property(x => x.Id).IsModified = false;
                attach.Property(x => x.Email).IsModified = true;
                int linhasAfetadas = await _context.SaveChangesAsync(); //Confirma a alteração no banco
                return Ok(linhasAfetadas); //Retorna as linhas afetadas (Geralmente sempre 1 linha msm)
            }
            catch (System.Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }


        //Método para alteração da foto
        [HttpPut("AtualizarFoto")]
        public async Task<IActionResult> AtualizarFoto(Usuario u)
        {
            try
            {
                Usuario usuario = await _context.TB_USUARIOS
                .FirstOrDefaultAsync(x => x.Id == u.Id);
                usuario.Foto = u.Foto;
                var attach = _context.Attach(usuario);
                attach.Property(x => x.Id).IsModified = false;
                attach.Property(x => x.Foto).IsModified = true;
                int linhasAfetadas = await _context.SaveChangesAsync();
                return Ok(linhasAfetadas);
            }
            catch (System.Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }


}