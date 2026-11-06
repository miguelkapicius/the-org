using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CSM.Sim;
using CSM.Map;
using CSM.View;
using CSM.Audio;   // sons (Som3D, da sessão de som): chamados junto de cada efeito visual

namespace CSM.View3D
{
    // PartidaAoVivo3D, parte: bonecos: animação, armas na mão e nas costas, mira, mortes, interpolação, sons por estado, efeitos e fumaças.
    public partial class PartidaAoVivo3D
    {
        // corpo andado por ciclo de passo (unidades do jogo) das passadas do exportar3d.py: 2 x passada / fração no chão x 25
        const float CICLO_DEVAGAR = 2 * 0.12f / 0.5f * 25, CICLO_AGACHADO = 2 * 0.1f / 0.5f * 25;

        readonly Dictionary<int, (float ini, float ate)> sacando = new Dictionary<int, (float, float)>();
        readonly Dictionary<int, int> ultHp = new Dictionary<int, int>();
        readonly Dictionary<int, float> luneta = new Dictionary<int, float>();
        readonly HashSet<int> escadaVista = new HashSet<int>();
        class Antes { public string gun; public int nades, troca, hp = 100; public bool bomba, noAr, vivo = true; public float quedaDe; }
        readonly Dictionary<int, Antes> antes = new Dictionary<int, Antes>();
        readonly Dictionary<int, (string anim, float ini, float dur)> evento = new Dictionary<int, (string, float, float)>();
        readonly Dictionary<int, float> trocouEm = new Dictionary<int, float>(), costasEm = new Dictionary<int, float>();

        readonly Dictionary<int, (string arma, GameObject go, Vector3 lp, Quaternion lr)> costas = new Dictionary<int, (string, GameObject, Vector3, Quaternion)>();
        /// <summary>Primária pendurada nas costas presa no tronco (null = esconde).</summary>
        void Costas(int id, Boneco3D b, string gunId)
        {
            string nome = gunId != null ? Armas3D.DoJogo(gunId) : null;
            costas.TryGetValue(id, out var c);
            if (nome == null) { if (c.go != null) c.go.SetActive(false); return; }
            if (c.go == null || c.arma != nome)
            {
                if (c.go != null) Apagar(c.go);
                var go = Armas3D.NasCostas(b.modelo, nome, Boneco3D.matCorpo, b.gameObject.layer, out var lp, out var lr);
                go.transform.SetParent(transform, false); c = (nome, go, lp, lr); costas[id] = c;
            }
            var tr = b.Peca("tronco"); if (tr == null) { c.go.SetActive(false); return; }
            c.go.SetActive(true);
            c.go.transform.SetPositionAndRotation(tr.position + tr.rotation * (c.lp * Boneco3D.ESCALA), tr.rotation * c.lr);
        }

        void Laser(Agent a, Boneco3D b)
        {
            V2 dir = V2.Ang(a.aim);
            float olho = a.z + NavGrid.EYE, len = MapData.Raio(a.pos, a.z, dir, 1600, NavGrid.EYE);
            if (m.SmokeBlocks(a.pos, olho, a.pos + dir * len, olho))
            {   // corta na borda da smoke (bisseção)
                float lo = 0, hi = len;
                for (int i = 0; i < 7; i++) { float mid = (lo + hi) * 0.5f; if (m.SmokeBlocks(a.pos, olho, a.pos + dir * mid, olho)) hi = mid; else lo = mid; }
                len = hi;
            }
            V2 f = a.pos + dir * len;
            float h = Mathf.Lerp(0.58f, 0.44f, Suave(a.duck));
            var pe = b.transform.position; var fim = MalhaMapa.ParaUnity(f.x, f.y, a.z) + Vector3.up * h;
            var plano = fim - pe; plano.y = 0; plano = plano.sqrMagnitude > 1e-6f ? plano.normalized : b.transform.forward;
            fim.y += Mathf.Tan(b.pitch * Mathf.Deg2Rad) * (len * MalhaMapa.ESC);
            // sai da ponta do cano da AWP (a peça de verdade na mão) e vai até onde a bala pega
            var boca = b.PontaDaArma("AWP", plano, out var pc) ? pc : pe + Vector3.up * h + plano * 0.62f;
            fx.Laser(a.id, boca, fim, a.Speed < 5 ? 1f : 0.35f);
        }

        /// <summary>Sem duelo: mira pra cima/baixo pro lugar que ele segura (alvoMira do Brain, quando é pra lá que ele
        /// olha: telhado, janela, 2º andar) ou, andando, pro chão uns 90 à frente (escada, rampa). Desnível pequeno = reto.</summary>
        float PitchMira(Agent a)
        {
            V2 olha = V2.Ang(a.aim);
            float Ang(V2 p, float l) { float dz = NavGrid.ZAt(p) - a.z; return Mathf.Abs(dz) < 5 ? 0 : Mathf.Clamp(Mathf.Atan2(dz, l) * Mathf.Rad2Deg, -60, 60); }
            if (a.brain.alvoMira is V2 am && NavGrid.Movable(am))
            {
                V2 d = am - a.pos; float l = d.Len;
                if (l > 30 && l < 900 && (d / l).Dot(olha) > 0.97f) return Ang(am, l);
            }
            V2 f = a.pos + olha * 90;
            if (NavGrid.Movable(f) && MapData.LosWalk(a.pos, a.z, f, NavGrid.ZAt(f), 18f)) return Ang(f, 90);
            return 0;
        }

        /// <summary>Indo pegar algo do chão: olha pra baixo pro item (como quem mira nele pro "E" do CS). Arma: ele encara
        /// e anda pra ela (o Brain faz Face no drop); granada pra ele e C4 (TR): anda por cima. Devolve o pitch (graus, ≤ 0).</summary>
        bool OlhaItem(Agent a, out float pitch)
        {
            V2 olha = V2.Ang(a.aim), anda = a.Speed > 5 ? a.vel / a.Speed : olha;
            float melhor = float.MaxValue, pv = 0;
            void Ver(V2 p, float z, bool encara)
            {
                V2 d = p - a.pos; float l = d.Len;
                if (l < 3 || l > 20 || l >= melhor || Mathf.Abs(z - a.z) > 10) return;   // só chegando nela (o E do CS)
                V2 u = d / l;
                if (u.Dot(anda) < 0.8f && l > 14) return;               // não está indo pra lá
                if (encara && u.Dot(olha) < 0.9f) return;               // arma: tem que estar encarando (~25°)
                melhor = l; pv = Mathf.Atan2(z - (a.z + NavGrid.EYE), l) * Mathf.Rad2Deg;
            }
            // só ARMA (pega com E, trocando a do slot): granada, kit e C4 se pegam passando por cima, sem olhar pro chão
            // (usuário: "no CS não dá pra usar o E, então é só passando em cima")
            foreach (var d in m.drops) Ver(d.pos, d.z, true);
            pitch = Mathf.Clamp(pv, -45, 0);
            return melhor < float.MaxValue;
        }
        int callsVistas; readonly int[] iglCallsVistas = new int[2];

        /// <summary>Eventos que viram animação curta: pegou arma/granada/C4 do chão ou largou o C4 (pegar), cantou no
        /// rádio (apontar), caiu de altura (aterrissar = agacha um instante).</summary>
        void Detectar()
        {
            foreach (var a in m.agents)
            {
                if (!antes.TryGetValue(a.id, out var o)) { antes[a.id] = new Antes { gun = a.gunId, nades = a.nades.Count, bomba = a.hasBomb, noAr = a.noAr, troca = a.trocouArma }; continue; }
                if (a.alive && m.t > 0.5f)
                {
                    // trocou de arma no chão (o "E" do CS): a dele sai jogada pra frente e a nova vem sacando, olhando pra baixo
                    if (a.gunId != o.gun && a.trocouArma == o.troca)   // (trocou pelo Q = só sacar, não é pegar do chão)
                    {
                        evento[a.id] = ("trocar", m.t, 0.6f); trocouEm[a.id] = m.t;
                        Debug.Log($"[Visual3D] trocou arma no chão: {a.name} {o.gun} -> {a.gunId} round {Round} t={m.t:0.0}");
                    }
                    // pegar item do chão não tem pose (usuário): o item vem como ímã (Imas). Largar a C4 também sem pose.
                    var pe = MalhaMapa.ParaUnity(a.pos.x, a.pos.y, a.z);
                    if (a.gunId != o.gun && a.trocouArma == o.troca) Som3D.Pegar(pe);
                    if (a.hasBomb && !o.bomba) Som3D.C4Pegar(pe); else if (!a.hasBomb && o.bomba && !m.bombPlanted) Som3D.C4Largar(pe);
                    if (!o.noAr && a.noAr) Som3D.Pulo(pe);
                    if (o.noAr && !a.noAr) { Som3D.Aterrissa(pe, Chao(a)); if (a.hp < o.hp) Som3D.Queda(pe); }   // perdeu vida ao cair = dano de queda
                    if (o.noAr && !a.noAr && o.quedaDe - a.z > 6) evento[a.id] = ("aterrissar", m.t, 0.2f);
                    // pousou (pulo de deslocamento, bunny hop ou queda): poeirinha nos dois pés
                    if (o.noAr && !a.noAr && bonecos.TryGetValue(a.id, out var bp))
                    {
                        var tr = bp.transform; var v = new Vector3(a.vel.x, 0, -a.vel.y) * MalhaMapa.ESC;
                        fx.Passo(tr.position + tr.right * 0.07f, v); fx.Passo(tr.position - tr.right * 0.07f, v);
                    }
                }
                o.gun = a.gunId; o.troca = a.trocouArma; o.nades = a.nades.Count; o.bomba = a.hasBomb; o.hp = a.hp; if (a.noAr && !o.noAr) o.quedaDe = a.quedaDe; o.noAr = a.noAr;
            }
            // calls no rádio (cada jogador) e do IGL: aponta pra frente
            if (m.callsFeitas.Count < callsVistas) callsVistas = 0;
            for (; callsVistas < m.callsFeitas.Count; callsVistas++)
            {
                var (t, by) = m.callsFeitas[callsVistas];
                if (by != null && by.alive && !evento.ContainsKey(by.id)) evento[by.id] = ("apontar", m.t, 0.9f);
            }
            for (int tm = 0; tm < 2; tm++)
            {
                var ig = m.igl[tm]; if (ig == null) continue;
                if (ig.calls.Count < iglCallsVistas[tm]) iglCallsVistas[tm] = 0;
                for (; iglCallsVistas[tm] < ig.calls.Count; iglCallsVistas[tm]++)
                {
                    var lider = m.agents.FirstOrDefault(x => x.alive && x.role == Role.Igl && (int)x.team == tm);
                    if (lider != null && !evento.ContainsKey(lider.id)) evento[lider.id] = ("apontar", m.t, 0.9f);
                }
            }
            foreach (var id in evento.Keys.ToList()) if (m.t - evento[id].ini > evento[id].dur) evento.Remove(id);
        }

        /// <summary>Como cai: de costas pra quem atirou de frente; de cara no chão se o tiro veio pelas costas.</summary>
        (Vector3 dir, float yaw, float forca, bool frente) Morte(Agent a, Boneco3D b)
        {
            if (mortes.TryGetValue(a.id, out var mo)) return mo;
            V2 de = a.killedBy != null ? a.killedBy.pos : a.pos - new V2(Mathf.Cos(a.aim), Mathf.Sin(a.aim));
            var d = new Vector3(a.pos.x - de.x, 0, -(a.pos.y - de.y)); d = d.sqrMagnitude > 1e-6f ? d.normalized : -b.transform.forward;
            var olha = b.transform.forward; olha.y = 0;
            bool frente = Vector3.Angle(olha, -d) > 110;   // quem matou está atrás dele
            float forca = a.killedBy != null && a.killedBy.gunId == "awp" ? 0.45f : 0.28f;
            mo = (d, Quaternion.LookRotation(frente ? d : -d).eulerAngles.y, forca, frente);
            mortes[a.id] = mo;
            return mo;
        }

        /// <summary>Escada de mão do janelão: a animação foi feita pra escada reta na frente do boneco; a do mapa é
        /// inclinada (sobe 36 em 20 de chão, ~29° da vertical). Inclina o corpo junto (gira pelo pé) e encosta ele nos
        /// degraus (o pé da animação fica 0,1 à frente da origem).</summary>
        public static void NaEscada(Transform t)
        {
            var esc = MapData.areas.Find(s => s.name == "Esc_janelao");
            float w = esc != null ? esc.box.x1 - esc.box.x0 : 20, h = MapData.UP + 6 - MapData.GZ;
            float incl = Mathf.Atan2(w, h) * Mathf.Rad2Deg;
            t.rotation = Quaternion.Euler(0, 270, 0) * Quaternion.Euler(incl, 0, 0);
            t.position -= Quaternion.Euler(0, 270, 0) * Vector3.forward * (0.1f * Boneco3D.ESCALA);
        }
        readonly Dictionary<int, (Vector3 dir, float yaw, float forca, bool frente)> mortes = new Dictionary<int, (Vector3, float, float, bool)>();
        // último tiro que cada um levou: na cabeça? de que direção (Unity)? quando — o tranco e o ragdoll reagem ao ponto
        readonly Dictionary<int, (bool hs, Vector3 dir, float t)> ultAcerto = new Dictionary<int, (bool, Vector3, float)>();
        readonly Dictionary<int, Ragdoll3D> ragdolls = new Dictionary<int, Ragdoll3D>();

        // interpolação: a simulação anda a 32 passos/s e o jogo desenha a 60+; o boneco fica entre a posição de antes do
        // último passo e a atual, na fração do tempo de desenho (relogio) dentro desse passo. Sem isso ele andava aos
        // trancos (quadro sem passo + quadro com dois) e parecia rodar a 20 fps.
        readonly Dictionary<int, (V2 p, float z, float mira)> antesPasso = new Dictionary<int, (V2, float, float)>();
        void GuardarAntes()
        {
            foreach (var a in m.agents) antesPasso[a.id] = (a.pos, a.z, a.aim);
            foreach (var n in m.flying) nadeAntes[n] = new Vector3(n.x, n.y, n.z);
        }
        // granada voando: a mesma interpolação entre passos (sem isso ela pulava a 32 Hz)
        readonly Dictionary<Match.Nade, Vector3> nadeAntes = new Dictionary<Match.Nade, Vector3>();
        Vector3 NadeVisivel(Match.Nade n)
        {
            var agora = new Vector3(n.x, n.y, n.z);
            if (!nadeAntes.TryGetValue(n, out var antes)) return agora;
            return Vector3.Lerp(antes, agora, Mathf.Clamp01(1 - (m.t - relogio) / Match.DT));
        }
        (V2 p, float z, float mira) Visivel(Agent a)
        {
            if (!antesPasso.TryGetValue(a.id, out var v)) return (a.pos, a.z, a.aim);
            float k = Mathf.Clamp01(1 - (m.t - relogio) / Match.DT);
            if ((a.pos - v.p).Len > 30) return (a.pos, a.z, a.aim);   // teleporte (round novo, escada): sem arrastar
            float d = Mathf.DeltaAngle(v.mira * Mathf.Rad2Deg, a.aim * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            return (v.p + (a.pos - v.p) * k, Mathf.Lerp(v.z, a.z, k), v.mira + d * k);
        }

        readonly Dictionary<int, string> animSom = new Dictionary<int, string>();   // última animação que tocou som
        // sons por estado: degrau da escada já tocado, quem está pisando no fogo, quem já ficou cego (zumbido do seguido)
        readonly Dictionary<int, int> escadaDegrau = new Dictionary<int, int>();
        int saqueCurto;   // diagnóstico: saques curtos pulados (log das 6 primeiras)
        readonly HashSet<int> noFogo = new HashSet<int>(), cegoVisto = new HashSet<int>(), quaseVazio = new HashSet<int>(), vendeu = new HashSet<int>();
        readonly List<Vector3> molotovsNoAr = new List<Vector3>();
        readonly List<(Vector3 p, bool inc, float t)> molotovsSumiram = new List<(Vector3, bool, float)>();   // de quem é o fogo novo
        /// <summary>Material onde a bala bateu (ponto Unity) pro som do impacto: caixa/carroça = madeira, piso pelo MapData.fmat,
        /// parede = pedra.</summary>
        static string MaterialEm(Vector3 u)
        {
            float gx = u.x / MalhaMapa.ESC, gy = -u.z / MalhaMapa.ESC, gz = u.y / MalhaMapa.ESC;
            int x = Mathf.Clamp((int)gx, 0, MapData.NX - 1), y = Mathf.Clamp((int)gy, 0, MapData.NY - 1);
            foreach (var c in MapData.covers)
                if (x >= c.r.x0 && x < c.r.x1 && y >= c.r.y0 && y < c.r.y1 && (c.kind.StartsWith("crate") || c.kind == "cart")) return "madeira";
            float chao = NavGrid.ZAt(new V2(gx, gy));
            if (gz <= chao + 1.5f) return MapData.fmat[x, y] switch { MapData.Floor.Dirt => "terra", MapData.Floor.Wood => "madeira", MapData.Floor.Cart => "madeira", _ => "pedra" };
            return "pedra";
        }

        /// <summary>Chão debaixo do pé pro som do passo: material do piso do mapa; em cima de caixa/carroça = madeira.</summary>
        static string Chao(Agent a)
        {
            int x = Mathf.Clamp((int)a.pos.x, 0, MapData.NX - 1), y = Mathf.Clamp((int)a.pos.y, 0, MapData.NY - 1);
            if (MapData.coverH[x, y] > 0 && a.z >= MapData.GZ + MapData.coverH[x, y] - 1) return "madeira";
            if (Pisos3D.Deck()[x, y]) return "madeira";   // deck da banca do B (Pisos3D: piso trocado só no visual)
            if (Marmore()[x, y] && MapData.fmat[x, y] == MapData.Floor.Stone) return "marmore";   // palácio: piso de mármore (só som)
            return MapData.fmat[x, y] switch { MapData.Floor.Dirt => "terra", MapData.Floor.Wood => "madeira", MapData.Floor.Cart => "madeira", _ => "pedra" };
        }
        static bool[,] marmore;
        /// <summary>Células das áreas do palácio (Floor.Stone na Sim; o som de passo/pulo é de mármore).</summary>
        static bool[,] Marmore()
        {
            if (marmore != null) return marmore;
            marmore = new bool[MapData.NX, MapData.NY];
            var nomes = new HashSet<string> { "Hall_palacio", "Esc_palacio", "Palacio", "Palacio_sala", "Palacio_corredor", "Sacada_A" };
            foreach (var s in MapData.areas)
            {
                if (!nomes.Contains(s.name)) continue;
                int x0 = Mathf.Max(0, s.box.x0), x1 = Mathf.Min(MapData.NX, s.box.x1), y0 = Mathf.Max(0, s.box.y0), y1 = Mathf.Min(MapData.NY, s.box.y1);
                for (int x = x0; x < x1; x++) for (int y = y0; y < y1; y++) if (s.has == null || s.has(x, y)) marmore[x, y] = true;
                Debug.Log($"[Visual3D] mármore: {s.name} ({x0},{y0})-({x1},{y1})");
            }
            return marmore;
        }
        // jeito do arremesso em curso (sufixo da animação: "", "_meio", "_baixo") e se é jump throw
        readonly Dictionary<int, (string suf, bool pula)> arremesso = new Dictionary<int, (string, bool)>();
        void Bonecos(float dt)
        {
            foreach (var a in m.agents)
            {
                var b = bonecos[a.id];
                if (!est.TryGetValue(a.id, out var e)) { e = new Transmissao.Anim { ultPos = a.pos }; est[a.id] = e; }
                if (a.brain.action != e.acao)
                {
                    if (e.acao == "granada" && m.t - e.acaoIni >= e.acaoDur * 0.85f && a.alive)
                        evento[a.id] = ("granada_fim" + (arremesso.TryGetValue(a.id, out var jt) ? jt.suf : ""), m.t, 0.4f);
                    e.acao = a.brain.action; e.acaoIni = m.t; e.acaoDur = Mathf.Max(0.2f, a.brain.ActionEnd - m.t);
                }
                var vis = Visivel(a);
                float dist = (vis.p - e.ultPos).Len; e.ultPos = vis.p;
                string arma = Arma(a), anim; float u = 0; bool porTempo = false;
                // arrependeu da compra (Match.vendas): segura a SMG desde o freeze até vender; no instante some, o "$" sobe
                // da etiqueta e ele saca a pistola
                if (a.alive && !a.faca && replay == null)
                    foreach (var vd in m.vendas)
                    {
                        if (vd.quem != a) continue;
                        if (m.t < vd.t) { string sm = vd.item == "mac10" ? "MAC10" : vd.item == "mp9" ? "MP9" : null; if (sm != null && b.Tem("mira_" + sm)) arma = sm; }
                        else if (vendeu.Add(a.id))
                        {
                            hud.Vendeu(m.agents.IndexOf(a), vd.item);
                            evento[a.id] = ("trocar", m.t, 0.45f);
                            if (seguir == a.id) Som3D.Hud("compra", 0.35f);
                            Debug.Log($"[Visual3D] vendeu {vd.item}: {a.name} round {rn} t={m.t:0.0}");
                        }
                    }
                float tiroDur = arma == "AWP" ? 1.1f : 0.25f;
                string acao = a.brain.action;
                // pernas: correm pra onde o boneco anda; tronco e arma ficam na mira. De costas = ciclo ao contrário.
                float alvoPerna = 0; bool anda = a.Speed > 6 && a.alive;
                if (anda)
                {
                    float d = Mathf.DeltaAngle(a.aim * Mathf.Rad2Deg, Mathf.Atan2(a.vel.y, a.vel.x) * Mathf.Rad2Deg);
                    bool costas = Mathf.Abs(d) > 105;
                    if (costas) d = Mathf.DeltaAngle(0, d + 180);
                    alvoPerna = Mathf.Clamp(d, -75, 75);
                    if (costas) dist = -dist;
                }
                bool naEscada = a.alive && !a.noAr && NavGrid.Escada(a.pos) && a.z > MapData.GZ + 1.5f;
                // passada do momento (pra camada das pernas quando a animação de cima é uma ação)
                string marcha = anda && !a.faca && !naEscada && !a.noAr ? (a.duck > 0.5f ? "agachado" : a.Speed < a.MaxSpeed * 0.62f ? "devagar" : "andar") : null;
                float passoAntes = e.passo;
                evento.TryGetValue(a.id, out var ev);
                if (!a.alive)
                {
                    // morreu: ragdoll de verdade a partir da pose em que estava (o corpo fica onde caiu; a arma voa à parte)
                    if (!ragdolls.TryGetValue(a.id, out var rg))
                    {
                        var mo = Morte(a, b);
                        // o piscar do último tiro zera: o corpo não posa mais (só o ragdoll), o branco ficaria preso nele
                        b.Golpe(0);
                        b.semArma = true; b.Pose(b.animAtual ?? "mira_" + arma, b.uAtual, 0);   // esconde a arma da mão
                        bool hsMorte = ultAcerto.TryGetValue(a.id, out var ua) && ua.hs && m.t - ua.t < 0.6f;
                        // embalo: velocidade de quando ainda estava vivo (a Sim pode zerar na morte)
                        velViva.TryGetValue(a.id, out var vv);
                        rg = new Ragdoll3D(b, mo.dir, mo.forca, a.id * 31 + rn, hsMorte, new Vector3(vv.x, 0, -vv.y) * MalhaMapa.ESC); ragdolls[a.id] = rg;
                        Som3D.Parar(a.id); Som3D.CorpoCaiu(b.transform.position);
                    }
                    rg.Passo(dt); Costas(a.id, b, null);
                    b.noChao = false; b.Silhueta(false); ultHp[a.id] = a.hp;
                    continue;
                }
                velViva[a.id] = a.vel;
                // escada de mão: pés degrau a degrau pela altura subida (2 degraus de 4 = 8 unidades por ciclo), arma nas costas
                if (naEscada)
                {
                    anim = "escada"; u = (a.z - MapData.GZ) / 8f;
                    // um som por degrau (2 degraus por ciclo de 8 de altura)
                    int dg = Mathf.FloorToInt(u * 2);
                    if (escadaDegrau.TryGetValue(a.id, out int dg0) && dg0 != dg) Som3D.Escada(b.transform.position);
                    escadaDegrau[a.id] = dg;
                    if (escadaVista.Add(a.id)) Debug.Log($"[Visual3D] escada: {a.name} round {rn} t={m.t:0.0}");
                }
                else if (acao == "plantar" || acao == "fake plant") { anim = "plantar"; u = (m.t - e.acaoIni) / (acao == "plantar" ? e.acaoDur : 3.2f); }
                else if (acao == "desarmar" || acao == "fake defuse") { anim = "desarmar"; u = m.t - e.acaoIni; porTempo = true; }
                else if (acao == "granada")
                {
                    string g = a.brain.AcaoGranada ?? "smoke";
                    // jeito do CS2 pela força que o Sim escolheu: esquerdo (1) por cima, os dois (0,5) meia força, direito por baixo;
                    // jump throw (Pulo/CorrePulo): a Sim pula na soltura (as pernas fazem a pose de pulo)
                    var pl = a.brain.PlanoGranada;
                    string suf = pl == null || pl.Value.forca >= 1 ? "" : pl.Value.forca >= 0.5f ? "_meio" : "_baixo";
                    bool pula = pl != null && (pl.Value.tipo == Arremesso.Tipo.Pulo || pl.Value.tipo == Arremesso.Tipo.CorrePulo);
                    arremesso[a.id] = (suf, pula);
                    anim = "granada_" + (g == "incendiaria" ? "molotov" : g) + suf; u = (m.t - e.acaoIni) / e.acaoDur;
                    // chegou na lineup com ela já na mão (pré-saque): o arremesso continua de onde a mão estava, sem sacar de novo
                    if (preSaque.TryGetValue(a.id, out float tn) && e.acaoIni - tn < 0.3f) u = U_NA_MAO + (1 - U_NA_MAO) * u;
                    if (!b.Tem(anim)) anim = "granada_" + (g == "incendiaria" ? "molotov" : g);
                }
                // pré-saque (Agent.nadeNaMao): andando até a lineup com a granada na mão, arma guardada — a pose de segurar do
                // começo da ação "granada" (sai da arma em ~0,35 s e fica). Se aparece inimigo a Sim guarda e o saque da arma vem
                else if (!string.IsNullOrEmpty(a.nadeNaMao) && !a.faca)
                {
                    string g = a.nadeNaMao;
                    anim = "granada_" + (g == "incendiaria" ? "molotov" : g);
                    u = U_NA_MAO * Mathf.Clamp01((m.t - a.nadeNaMaoDesde) / 0.35f);
                    preSaque[a.id] = m.t;
                }
                else if (ev.anim != null && ev.anim.StartsWith("granada_fim"))
                {
                    anim = b.Tem(ev.anim) ? ev.anim : "granada_fim"; u = (m.t - ev.ini) / ev.dur;
                    // a arma ficou guardada no fim do arremesso: emenda o saque (fuzil por cima do ombro, pistola do coldre)
                    if (u >= 1 && !a.faca)
                    {
                        bool costas = !WeaponStats.Pistola(a.gunId) && b.Tem("sacar_costas_" + arma);
                        evento[a.id] = (costas ? "costas" : "saque", m.t, costas ? 0.7f : 0.45f);
                        anim = (costas ? "sacar_costas_" : "sacar_") + arma; u = 0;
                    }
                }
                else if (ev.anim == "saque" && !a.faca) { anim = "sacar_" + arma; u = (m.t - ev.ini) / ev.dur; }
                else if (ev.anim == "costas" && !a.faca) { anim = "sacar_costas_" + arma; u = (m.t - ev.ini) / ev.dur; }
                // cego atirando no vento (Sim: o flashado recua e pode atirar andando): no tique do tiro a arma vem pra frente
                else if (a.Blind(m.t) && (a.faca || m.t - a.lastShotT >= tiroDur)) { anim = b.Tem("cego_" + arma) ? "cego_" + arma : "cego"; u = m.t; porTempo = true; }
                else if (ev.anim == "pegar" && !a.faca) { anim = "pegar_" + arma; u = (m.t - ev.ini) / ev.dur; }
                else if (ev.anim == "trocar" && !a.faca) { anim = "sacar_" + arma; u = (m.t - ev.ini) / ev.dur; }
                else if (!a.faca && a.gun.Reloading) { anim = "recarga_" + arma; u = 1 - a.gun.reloadLeft / a.gun.s.Reload; }
                else if (!a.faca && m.t - a.lastShotT < tiroDur) { anim = "tiro_" + arma; u = (m.t - a.lastShotT) / tiroDur; }
                else if (a.noAr) anim = (a.faca ? "faca_" : "") + (a.vz > 0 ? "no_ar_sobe" : "no_ar_cai") + (a.faca ? "" : "_" + arma);
                // meio cego (flash de lado): aba com a mão na testa, rosto virado, arma ainda na frente
                else if (!a.faca && a.MeioCego(m.t) && b.Tem("meio_cego_" + arma)) { anim = "meio_cego_" + arma; u = m.t; porTempo = true; }
                else if (a.faca) { anim = anda ? "faca_andar" : "faca_parado"; e.passo += dist / Transmissao.Ciclo("faca_andar"); u = e.passo; }
                else if (anda && a.duck > 0.5f) { anim = "agachado_" + arma; e.passo += dist / CICLO_AGACHADO; u = e.passo; }
                else if (anda && a.Speed < a.MaxSpeed * 0.62f) { anim = "devagar_" + arma; e.passo += dist / CICLO_DEVAGAR; u = e.passo; }
                else if (anda) { anim = "andar_" + arma; e.passo += dist / Transmissao.Ciclo("andar"); u = e.passo; }
                else if (arma == "AWP" && a.scoped)
                {
                    // entrando na luneta (0,27 s) e depois parado nela
                    if (!luneta.TryGetValue(a.id, out float li)) { li = m.t; luneta[a.id] = li; }
                    float ul = (m.t - li) / 0.27f;
                    if (ul < 1) { anim = "awp_mirar"; u = ul; } else anim = "awp_mirando";
                }
                else if (ev.anim == "apontar" && !a.faca) { anim = "apontar_" + arma; u = (m.t - ev.ini) / ev.dur; }
                else if (ev.anim == "aterrissar") anim = (a.faca ? "faca_parado" : "agachar_" + arma);
                else anim = (a.Agachado ? "agachar_" : "mira_") + arma;
                if (!(arma == "AWP" && a.scoped)) luneta.Remove(a.id);
                // sacando a arma (trocou ou guardou a faca): a pose de sacar por cima de parado/andando
                if (!a.faca && a.alive && !anda && a.sacaAte > m.t && string.IsNullOrEmpty(acao) && a.nadeNaMao == null && !a.gun.Reloading && !a.noAr
                    && !a.Blind(m.t) && m.t - a.lastShotT > tiroDur && !anim.StartsWith("sacar_costas"))
                {
                    if (!sacando.TryGetValue(a.id, out var sc) || sc.ate != a.sacaAte)
                    {
                        sc = (m.t, a.sacaAte); sacando[a.id] = sc;
                        if (sc.ate - sc.ini < 0.25f && saqueCurto++ < 6) Debug.Log($"[Visual3D] saque curto pulado: {a.name} {arma} sobrava {sc.ate - sc.ini:0.00}s round {rn} t={m.t:0.0}");
                    }
                    // saque que começou andando e sobrou pouco quando parou (usuário: "antes de atirar a mão dá uma tremidinha
                    // e vai pra arma"): a animação inteira cabia no resto — a mão descia e voltava num piscar. Com menos de
                    // 0,25 s de saque pela frente fica na mira (a arma já está quase na mão)
                    if (sc.ate - sc.ini >= 0.25f)
                    {
                        // primária com inventário (veio da pistola pelo Q): sai das costas por cima do ombro
                        bool dasCostas = !WeaponStats.Pistola(a.gunId) && a.TemOutra && b.Tem("sacar_costas_" + arma);
                        anim = (dasCostas ? "sacar_costas_" : "sacar_") + arma; u = (m.t - sc.ini) / Mathf.Max(0.1f, sc.ate - sc.ini);
                    }
                }
                // mira pra cima/baixo: pro alvo do duelo (telhado, 2º andar, escada)
                float pAlvo = 0;
                var alvoA = a.alive ? a.brain.target : null;
                if (alvoA != null)
                {
                    float hz = (alvoA.pos - a.pos).Len;
                    pAlvo = Mathf.Clamp(Mathf.Atan2(alvoA.z - a.z, Mathf.Max(hz, 1)) * Mathf.Rad2Deg, -60, 60);
                }
                // sem duelo: olha pro chão só chegando numa ARMA pra pegar com E (trocando a do slot); granada/kit/C4 não
                // (usuário: ficava "olhando pro chão o tempo todo" depois de uma troca, com os drops em volta)
                else if (a.alive && string.IsNullOrEmpty(acao) && OlhaItem(a, out float pItem)) pAlvo = pItem;
                else if (a.alive) pAlvo = PitchMira(a);
                // (trocar arma no chão não olha mais pra baixo: a nova vem como ímã, usuário)
                b.pitch = Mathf.Lerp(b.pitch, pAlvo, 1 - Mathf.Exp(-10f * dt)); b.armaPitch = arma;
                if (!b.Tem(anim)) anim = "mira_" + Transmissao.Padrao(!a.IsT);
                // agachando/levantando aos poucos (Agent.duck da Sim, com smoothstep): parado, a mira puxa pra pose agachada
                float dsu = Suave(a.duck);
                string agA = "agachar_" + arma; if (!b.Tem(agA)) agA = "agachar_" + Transmissao.Padrao(!a.IsT);
                b.agachaAnim = null; b.agachaW = 0;
                if (anim.StartsWith("mira_") && dsu > 0.001f && a.alive) { b.agachaAnim = agA; b.agachaW = dsu; b.agachaSoPernas = false; }
                // ação de tronco andando: as pernas seguem a passada (recarregar/atirar/sacar/jogar granada correndo)
                bool acaoTronco = anim.StartsWith("recarga") || anim.StartsWith("tiro") || anim.StartsWith("sacar") || anim.StartsWith("granada") || anim.StartsWith("apontar") || anim.StartsWith("cego") || anim.StartsWith("meio_cego");
                if (marcha != null && acaoTronco)
                {
                    float ciclo = marcha == "agachado" ? CICLO_AGACHADO : marcha == "devagar" ? CICLO_DEVAGAR : Transmissao.Ciclo("andar");
                    if (e.passo == passoAntes) e.passo += dist / ciclo;
                    string pa = marcha + "_" + arma; if (!b.Tem(pa)) pa = marcha + "_" + Transmissao.Padrao(!a.IsT);
                    b.pernaAnim = pa; b.pernaU = e.passo;
                }
                else
                {
                    b.pernaAnim = null;
                    // agachado parado atirando/recarregando: as pernas descem pra pose agachada com o duck e o tronco junto
                    if (dsu > 0.001f && a.alive && acaoTronco && !a.noAr) { b.agachaAnim = agA; b.agachaW = dsu; b.agachaSoPernas = true; }
                }
                b.troncoY = acaoTronco ? -0.1f * dsu : 0;
                // jump throw: a Sim pula de verdade no tique em que a granada sai da mão (me.noAr); o tronco segue o arremesso
                // (granada_fim) e as pernas fazem a pose de pulo, subindo e caindo. (O pulinho só visual daqui saiu: dobrava.)

                if (a.noAr && acaoTronco && b.Tem("pular")) { b.pernaAnim = "pular"; b.pernaU = a.vz > 0 ? 0.35f : 0.7f; }
                // pulo agachado do CS (Agent.puloAgachado: subindo degrau de 18-23, ex.: a carroça do B): no ar encolhe as
                // pernas na pose agachada
                if (a.alive && a.noAr && a.puloAgachado) { b.agachaAnim = agA; b.agachaW = 1; b.agachaSoPernas = true; }
                // pezinho (boost) é como no CS: o de baixo só agacha (Agent.duck, pode levantar pro pezinho em pé) e o de cima
                // fica em pé na cabeça dele na altura que a Sim der, sem pose própria
                // meio cego (flash de lado): um pouco da pose de cego por cima, sumindo
                b.misturaW = 0;
                // cego não pinta o boneco de branco (personagem é personagem): quem está perto da flash recebe a luz do estouro
                b.brancoCego = 0;
                // pente quase vazio (~20%): 1 vez por pente
                if (a.alive && !a.faca && a.gun != null && a.gun.s.mag > 4)
                {
                    if (a.gun.inMag > 0 && a.gun.inMag <= Mathf.CeilToInt(a.gun.s.mag * 0.2f)) { if (quaseVazio.Add(a.id)) Som3D.QuaseVazio(b.transform.position); }
                    else if (a.gun.inMag > a.gun.s.mag * 0.5f) quaseVazio.Remove(a.id);
                }
                bool queima = a.alive && m.NoFogo(a);    // a mesma regra da Sim: queima quem encosta o corpo na chama desenhada (-exp bxFogoCorpoVelho volta ao pé na célula)
                if (queima) { fx.Queimando(b.transform.position, dt); if (noFogo.Add(a.id)) Som3D.Queimando(b.transform.position, a.id); }
                else noFogo.Remove(a.id);
                // zumbido da flash só de quem a câmera segue (é o "ouvido" do espectador)
                bool cego = a.alive && a.Blind(m.t);
                if (cego && seguir >= 0 && m.agents[seguir] == a && cegoVisto.Add(a.id)) Som3D.Zumbido(1f);
                else if (!cego) cegoVisto.Remove(a.id);
                // perna só gira em animação de passo (na ação/no ar volta pro corpo)
                bool passo = anim.StartsWith("andar") || anim.StartsWith("devagar") || anim.StartsWith("agachado") || anim == "faca_andar" || b.pernaAnim != null;
                b.yawPernas = Mathf.LerpAngle(b.yawPernas, passo ? alvoPerna : 0, 1 - Mathf.Exp(-12f * dt));
                // arma grande nas costas (plantar, desarmar, escada, faca): na volta ele puxa das costas por cima do ombro
                bool nasCostas = anim.StartsWith("plantar") || anim.StartsWith("desarmar") || anim == "escada" || anim.StartsWith("faca");
                if (nasCostas) costasEm[a.id] = m.t;
                else if (!a.faca && costasEm.TryGetValue(a.id, out float tc) && m.t - tc < 0.25f && b.Tem("sacar_costas_" + arma) && !anim.StartsWith("tiro"))
                {
                    costasEm.Remove(a.id); evento[a.id] = ("costas", m.t, 0.7f);
                    anim = "sacar_costas_" + arma; u = 0; porTempo = false;
                }
                else costasEm.Remove(a.id);
                // a C4 é a plantada no chão: desarmando e no fim do plantar (a animação ainda roda uns quadros depois de
                // m.bombPlanted e mostrava duas C4: a da mão e a do chão)
                b.semC4 = anim.StartsWith("desarmar") || (anim.StartsWith("plantar") && m.bombPlanted);
                if (!animSom.TryGetValue(a.id, out var anAnt) || anAnt != anim)
                {
                    animSom[a.id] = anim; var pa = b.transform.position;
                    if (anim.StartsWith("recarga") && a.gun != null) Som3D.Recarga(pa, a.gunId, a.gun.s.Reload, a.id);
                    else if (anim == "sacar_faca") Som3D.FacaSacar(pa, a.id);
                    else if (anim.StartsWith("sacar") || anim.StartsWith("pegar")) Som3D.Sacar(pa, a.gunId, a.id);
                    else if (anim.StartsWith("granada_fim")) Som3D.Arremesso(pa, a.id);
                    else if (anim.StartsWith("granada_molotov") && a.IsT) { Som3D.GranadaSacar(pa, a.id); Som3D.Isqueiro(pa, a.id); }
                    else if (anim.StartsWith("granada_")) { Som3D.GranadaSacar(pa, a.id); Som3D.Pino(pa, a.id); }
                    else if (anim.StartsWith("plantar")) { Som3D.C4Sacar(pa); Som3D.C4Digita(pa); }
                    else if (anim.StartsWith("desarmar")) Som3D.C4Desarme(pa, a.hasKit);
                    else if (anim == "awp_mirar") Som3D.Zoom(pa);
                    else if (anim == "faca_m2") Som3D.FacaEstocada(pa, a.id);
                    else if (anim.StartsWith("faca_m")) Som3D.Faca(pa, false, a.id);
                    if (anAnt != null && anAnt.StartsWith("recarga") && !anim.StartsWith("recarga")) Som3D.Parar(a.id);   // trocou no meio
                }
                // arma guardada (granada, faca, plantar, desarmar, escada): essas animações foram exportadas com o fuzil
                // padrão do time (AK/M4) pendurado nas costas -> esconde o da pose e pendura a de VERDADE (usuário: de
                // pistola, vinha uma AK pras costas). Sem primária, nada nas costas (a pistola fica no coldre).
                bool guardada = anim.StartsWith("granada") || anim.StartsWith("faca") || anim.StartsWith("plantar") || anim.StartsWith("desarmar") || anim == "escada";
                if (guardada) b.semArma = true;
                if (porTempo) b.Tocar(anim, u, dt); else b.Pose(anim, u, dt);
                // primária guardada nas costas (de pistola na mão; no saque das costas a própria animação leva ela)
                string prim = a.Primaria;
                Costas(a.id, b, prim != null && (guardada || prim != a.gunId) && !anim.StartsWith("sacar_costas") ? prim : null);
                // molotov acesa na mão: o mesmo fogo do chão no pano + luz que ilumina quem segura
                if (anim.StartsWith("granada_molotov") && b.PecaVisivel("fogo0", out var pano)) fx.Chama(pano, dt, Mathf.Clamp01((u - 0.3f) / 0.15f));
                // laser da AWP na luneta (como nas transmissões): da arma até a parede ou a smoke; forte travado parado
                if (arma == "AWP" && a.scoped) Laser(a, b);
                b.noChao = !naEscada && !a.noAr;
                b.Silhueta(a.alive);
                // levou tiro (vida caiu): tranco + piscar, pela força do dano
                if (ultHp.TryGetValue(a.id, out int hp0) && a.hp < hp0 && a.alive)
                {
                    bool hsG = ultAcerto.TryGetValue(a.id, out var ug) && ug.hs && m.t - ug.t < 0.3f;
                    // de onde veio: pela frente joga pra trás, pelas costas joga pra frente
                    float sinal = ultAcerto.TryGetValue(a.id, out var ud) && m.t - ud.t < 0.3f ? (Vector3.Dot(b.transform.forward, ud.dir) < 0 ? 1 : -1) : 1;
                    b.Golpe(0.35f + (hp0 - a.hp) / 60f, hsG, sinal);
                }
                ultHp[a.id] = a.hp;
                // posição do pé e rumo da mira (interpolados entre passos da simulação: Visivel)
                b.transform.position = MalhaMapa.ParaUnity(vis.p.x, vis.p.y, vis.z);
                float miraV = vis.mira;
                // desarmando: de frente pra C4 (usuário: "o ct defusar olhando para a c4"; a mira da Sim às vezes ficava
                // olhando pro lado de onde viria o TR e ele mexia na bomba de costas pra ela)
                if (anim.StartsWith("desarmar") && m.bombPlanted)
                {
                    var dc = m.bombPos - vis.p;
                    if (dc.Len > 0.5f) miraV = Mathf.Atan2(dc.y, dc.x);
                }
                float alvoR = naEscada ? 270f : Mathf.Atan2(Mathf.Cos(miraV), -Mathf.Sin(miraV)) * Mathf.Rad2Deg;
                float r = rumo.TryGetValue(a.id, out float r0) ? Mathf.LerpAngle(r0, alvoR, 1 - Mathf.Exp(-25f * dt)) : alvoR;
                if (!a.alive) r = r0;
                rumo[a.id] = r;
                b.transform.rotation = Quaternion.Euler(0, r, 0);
                if (naEscada) NaEscada(b.transform);
                b.semArma = false;   // (morto vira ragdoll lá em cima)
            }
        }

        void Efeitos()
        {
            fumaca.Inicio(); fumacaCasca.Inicio(); fogo.Inicio(); rastro.Inicio(); coisas.Inicio();
            // smoke volumétrica: a densidade da simulação (crescimento, bolha da HE, desmanche) + furos de bala do desenho
            if (replay != null && rq0 != null) FumacasReplay(); else Fumacas();
            // rastro de tiro: risco fino que corre da boca do cano pro alvo (não um bastão inteiro piscando). Pelo registro dos
            // tiros (tirosGravados), no relógio da partida ou no do replay: antes saía do centro do boneco e sumia no replay
            float agora = replay != null ? replay.t : m.t;
            foreach (var s in tirosGravados)
            {
                float idade = agora - s.t; if (idade > 0.09f || idade < 0) continue;
                var a = s.boca; var b = s.ate;   // (sem atirador achado, boca = origem do tiro na simulação)
                var d = b - a; float L = d.magnitude; if (L < 1e-3f) continue;
                float cab = Mathf.Clamp01(idade / 0.05f), rabo = Mathf.Clamp01((idade - 0.025f) / 0.065f);
                var p0 = a + d * rabo; var p1 = a + d * cab; float l = Mathf.Max(0.02f, (p1 - p0).magnitude);
                rastro.Por((p0 + p1) * 0.5f, Quaternion.LookRotation(d), new Vector3(0.018f, 0.018f, l));
            }
            Props();

            // replay: a C4 do chão só existe do plant até a explosão (antes aparecia em lance de antes do plant: o TR
            // plantando com a C4 na mão e a plantada já no chão = duas)
            bool c4NoReplay = replay == null || replay.t >= c4PlantouEm && (c4ExplodiuEm < 0 || replay.t < c4ExplodiuEm);
            if (m.bombPlanted && !c4NoReplay && c4 != null) c4.SetActive(false);
            if (m.bombPlanted && c4NoReplay)
            {
                V2 bp = m.bombPos;
                float z = m.bombPlanted ? m.bombZ : NavGrid.ZAt(bp);
                if (c4 == null) { c4 = Armas3D.Prop(Boneco3D.Carregar("tr"), "plantar", 0.97f, Boneco3D.matCorpo); c4.transform.SetParent(transform, false); }
                c4.SetActive(true);
                c4.transform.SetPositionAndRotation(MalhaMapa.ParaUnity(bp.x, bp.y, z) + Vector3.up * 0.045f, Quaternion.Euler(0, 20, 0));
                // bip (Som3D, ritmo do CS) e o LED pisca junto com ele
                var posC4 = MalhaMapa.ParaUnity(bp.x, bp.y, z);
                if (replay == null) Som3D.C4(posC4, m.bombPlanted && !m.defused && !m.exploded, m.BombLeft);   // (bip só ao vivo)
                // LED vermelho em cima da C4 (antes era uma bolota de fogo laranja flutuando 5,5 acima: usuário "luz da C4 não
                // tá legal"): mesma agenda do bip, calculada aqui (vale no replay também), clarão curto e um brilho fraco aceso
                float restante = replay != null ? Match.BOMB - (replay.t - c4PlantouEm) : m.BombLeft;
                bool armada = replay != null ? c4PlantouEm >= 0 && (c4ExplodiuEm < 0 || replay.t < c4ExplodiuEm) && (c4DesarmouEm < 0 || replay.t < c4DesarmouEm) && !(m.defused && replay.t >= m.t - 0.1f)
                                             : !m.defused && !m.exploded;
                LedC4(armada && restante > 0 ? DesdeBip(restante, Match.BOMB) : -1);
            }
            if (c4Led != null && (c4 == null || !c4.activeSelf || !m.bombPlanted)) c4Led.SetActive(false);
            fumaca.Fim(); fumacaCasca.Fim(); fogo.Fim(); rastro.Fim(); coisas.Fim();
            fx.FimChamas();
            fx.FimLasers();
        }

        readonly Dictionary<SmokeVolume, Fumaca3D> fumacas = new Dictionary<SmokeVolume, Fumaca3D>();
        Shader shFumaca;
        void Fumacas() => Fumacas(m.smokes, m.t);
        void Fumacas(List<SmokeVolume> lista, float t)
        {
            if (shFumaca == null) shFumaca = Shader.Find("CSM/FumacaVolume");
            foreach (var s in fumacas.Keys.ToList())
                if (!lista.Contains(s)) { fumacas[s].Apagar(); fumacas.Remove(s); if (lista == m?.smokes) Som3D.SmokeFim(MalhaMapa.ParaUnity(s.origin.x, s.origin.y, s.origin.z)); }
            foreach (var s in lista)
            {
                if (!fumacas.TryGetValue(s, out var f))
                {
                    f = new Fumaca3D(s, transform, shFumaca); fumacas[s] = f;
                    // no replay só toca o estouro se a smoke nasce no lance (não quando ela é refeita no corte)
                    if (lista == m?.smokes || s.age < 0.3f) Som3D.Smoke(MalhaMapa.ParaUnity(s.origin.x, s.origin.y, s.origin.z));
                }
                f.Atualizar(t, cen.cam);
            }
            Fumaca3D.Publicar(t);
        }
    }
}
