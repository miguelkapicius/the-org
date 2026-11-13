using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSM.Map;

namespace CSM.Sim
{
    /// <summary>
    /// CÉREBRO NOVO, tijolo 1 (2026-11-11): a LEITURA COLETIVA do round. O Gabriel: "jogam no automático, pensam tudo igual, ninguém
    /// reage quando um morre, quero jogo coletivo". O cérebro velho (Brain + Igl) decide por centenas de regras locais; não existe
    /// um lugar só onde o time sabe "como está o round". Aqui existe: uma foto do round do ponto de vista do TIME (só com o que o
    /// time sabe — vivos, relógio, C4, crença de onde o inimigo está, onde cada um de nós está, quem está sem troca, que buraco abriu
    /// quando alguém caiu). É só leitura: nada aqui manda em ninguém. A decisão nova (postura do time → função de cada um → ação)
    /// vai ser escrita em cima desta foto, e a caixa-preta grava a foto pra conferir com o olho antes de confiar nela.
    /// </summary>
    public class Coletivo
    {
        public static readonly string[] ZONAS = { "A", "Mid", "B" };
        readonly Match m; readonly Team team; readonly TeamBrain igl;

        // ---- a foto (refeita 2x por segundo)
        public int nos, eles;                                   // vivos
        public int Vantagem => nos - eles;
        public string momento = "abertura";                     // abertura · default · contato · execução · pós-plant · retake · save
        public readonly float[] delesNa = new float[3];         // inimigos que o time ACREDITA em cada zona (crença, não a verdade)
        public readonly float[] vistosNa = new float[3];        // só os VISTOS há pouco em cada zona (quem está na minha cara agora; delesNa conta também os ouvidos)
        public readonly int[] nossosNa = new int[3];
        public readonly List<Agent> semTroca = new List<Agent>();   // vivo sem colega que chegue pra trocar em ~3 s
        public int buraco = -1;                                 // zona que ficou sem ninguém nosso e onde acreditamos ter inimigo (CT)
        public float ultimaBaixaT = -99; public V2 ultimaBaixaPos; public Agent ultimaBaixaPor; public int ultimaBaixaZona = -1;
        public float ultimoContatoT = -99;
        public readonly List<(float t, V2 pos, Agent por, int zona)> baixas = new List<(float, V2, Agent, int)>();   // todas as baixas do time no round, na ordem
        public const float EXEC = 2f;   // inimigos acreditados num bomb pra chamar de execução (o IGL e o "momento" usam o mesmo)
        /// <summary>Quantos acreditados num lado fazem dele uma execução. Com a crença 2 (que conta os ouvidos) dois no lado do A é o default
        /// de todo round (rampa + palácio): execução é a MAIORIA dos vivos deles ali (3 de 5, 2 de 3).</summary>
        public float ExecMin => m.intel[(int)team].Cr2 ? MathF.Max(2f, 0.6f * eles) : EXEC;
        public string postura = "";                             // o que a situação pede do time (ainda não manda em ninguém)
        public string posturaPorque = "";
        int abatesVistos; string ultimoTexto = "";

        public Coletivo(Match m, Team team, TeamBrain igl) { this.m = m; this.team = team; this.igl = igl; }

        public static int ZonaIdx(V2 p) { string z = Places.Zona(p); return z == "A" ? 0 : z == "B" ? 2 : 1; }   /* base conta como meio: ainda não escolheu lado */

        public void Atualiza()
        {
            float t = m.t; var it = m.intel[(int)team]; Team outro = team == Team.T ? Team.CT : Team.T;
            var meus = new List<Agent>(5); foreach (var a in m.agents) if (a.alive && a.team == team) meus.Add(a);
            nos = meus.Count; eles = m.Alive(outro);
            Array.Clear(nossosNa, 0, 3); Array.Clear(delesNa, 0, 3); Array.Clear(vistosNa, 0, 3);
            foreach (var a in meus) nossosNa[ZonaIdx(a.pos)]++;
            bool cr2 = it.Cr2;
            if (cr2) it.PorZona(delesNa, t, 12f);   /* visto, ouvido ou cantado: 4 correndo na rampa são 4, não "1 visto" */
            foreach (var c in it.contacts.Values)
            {
                if (!m.agents[c.id].alive) continue;
                float v = it.Vale(c, t); if (v < 0.05f) continue;
                vistosNa[ZonaIdx(c.pos)] += v; if (!cr2) delesNa[ZonaIdx(c.pos)] += v;
                if (t - c.t < 0.6f) ultimoContatoT = t;
            }
            // baixas novas do time: onde, pra quem, e que zona ficou aberta
            for (; abatesVistos < m.abates.Count; abatesVistos++)
            {
                var ab = m.abates[abatesVistos]; if (ab.v == null || ab.v.team != team || ab.suicidio) continue;
                ultimaBaixaT = ab.t; ultimaBaixaPos = ab.vPos; ultimaBaixaPor = ab.aliado ? null : ab.by; ultimaBaixaZona = ZonaIdx(ab.vPos);
                baixas.Add((ab.t, ab.vPos, ultimaBaixaPor, ultimaBaixaZona));
            }
            // sem troca: nenhum colega a menos de ~3 s de caminho (o pro joga em par ou cruzado; sozinho só o lurk e o âncora, de propósito)
            semTroca.Clear();
            foreach (var a in meus)
            {
                bool tem = false;
                foreach (var b in meus) if (b != a && (b.pos - a.pos).Len < 520) { tem = true; break; }   /* distância reta: consulta de caminho aqui gastava o orçamento do A* do jogo só pra um texto */
                if (!tem && nos > 1) semTroca.Add(a);
            }
            buraco = -1;
            if (team == Team.CT && !m.bombPlanted)
                for (int z = 0; z < 3; z += 2) if (nossosNa[z] == 0 && delesNa[z] >= 0.8f) buraco = z;
            // momento do round
            bool contato = t - ultimoContatoT < 6f || t - ultimaBaixaT < 6f || m.abates.Count > 0 && t - m.abates[m.abates.Count - 1].t < 6f;
            if (m.bombPlanted) momento = team == Team.T ? "pós-plant" : "retake";
            else if (team == Team.T && igl.Executou) momento = "execução";
            else if (team == Team.CT && (delesNa[0] >= ExecMin || delesNa[2] >= ExecMin)) momento = "execução";
            else if (contato) momento = "contato";
            else momento = t < 15f ? "abertura" : "default";
            Postura(t);
        }

        /// <summary>O que a situação pede do time. Pesos de situação (vantagem, relógio, C4), ainda sem perfil de IGL nem erro de jogador.</summary>
        void Postura(float t)
        {
            int v = Vantagem; float resta = m.RoundLeft;
            if (nos == 0) { postura = ""; posturaPorque = ""; return; }
            if (team == Team.CT)
            {
                if (m.bombPlanted) { postura = (TeamBrain.Retake2 ? TeamBrain.ChancePlacar(nos, eles) < TeamBrain.SAVE_ABAIXO : v <= -2 && m.BombLeft < 25) ? "guardar arma" : "juntar e retomar"; posturaPorque = $"{nos}x{eles}, {m.BombLeft:0}s de bomba"; }
                else if (v >= 1) { postura = "segurar a vantagem"; posturaPorque = $"{nos}x{eles}: o relógio é nosso, cruzado e sem duelo sozinho"; }
                else if (v <= -2) { postura = "recuar e jogar junto"; posturaPorque = $"{nos}x{eles}: sozinho num bomb não segura — par, retake"; }
                else if (buraco >= 0) { postura = "fechar o buraco"; posturaPorque = $"ninguém no {ZONAS[buraco]} e eles estão lá"; }
                else { postura = "setup"; posturaPorque = "cada um no seu lugar, info antes de girar"; }
            }
            else
            {
                if (m.bombPlanted) { postura = v >= 1 ? "segurar o pós-plant (tempo)" : "pós-plant cruzado"; posturaPorque = $"{nos}x{eles}, {m.BombLeft:0}s"; }
                else if (resta < 30) { postura = "ir pro bomb agora"; posturaPorque = $"{resta:0}s de round"; }
                else if (v >= 1) { postura = "juntar e entrar com troca"; posturaPorque = $"{nos}x{eles}: vantagem se joga em grupo, com granada, sem abrir um por um"; }
                else if (v <= -2) { postura = "pick ou save"; posturaPorque = $"{nos}x{eles}"; }
                else if (momento == "contato") { postura = "trocar ou sair da linha"; posturaPorque = "contato: ou troca na hora ou não entra na mesma linha"; }
                else { postura = "default"; posturaPorque = "espaço e informação antes de escolher o bomb"; }
            }
        }

        /// <summary>Uma linha pro registro (caixa-preta, painel). Null se não mudou desde a última.</summary>
        public string TextoSeMudou()
        {
            var sb = new StringBuilder(96);
            sb.Append(nos).Append('x').Append(eles).Append(" · ").Append(momento).Append(" · ").Append(postura);
            sb.Append(" · nós A/M/B ").Append(nossosNa[0]).Append('/').Append(nossosNa[1]).Append('/').Append(nossosNa[2]);
            sb.Append(" · eles ").Append(delesNa[0].ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Append('/')
              .Append(delesNa[1].ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Append('/')
              .Append(delesNa[2].ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
            if (semTroca.Count > 0) { sb.Append(" · sem troca:"); foreach (var a in semTroca) sb.Append(' ').Append(a.name); }
            if (buraco >= 0) sb.Append(" · buraco ").Append(ZONAS[buraco]);
            string s = sb.ToString(); if (s == ultimoTexto) return null; ultimoTexto = s; return s;
        }
    }
}
