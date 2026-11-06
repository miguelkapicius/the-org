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
    // PartidaAoVivo3D, parte: laboratório (Visual3D.CapturarLabBatch): smoke, tiro, HE, molotov, C4 e itens sem depender da partida.
    public partial class PartidaAoVivo3D
    {
        // ------------------------------------------------------------------ laboratório (Visual3D.CapturarLabBatch)
        // smoke, tiro, HE, molotov, C4 e itens no chão sem depender da partida: mesmo desenho e mesma física do jogo
        readonly List<SmokeVolume> labSmokes = new List<SmokeVolume>();
        readonly List<FireArea> labFogos = new List<FireArea>();
        readonly HashSet<object> labItens = new HashSet<object>();
        readonly List<(Vector3 a, Vector3 b, float t)> labTiros = new List<(Vector3, Vector3, float)>();
        public float LabT;
        public SmokeVolume LabSmoke(V3 o) { var s = new SmokeVolume(o); labSmokes.Add(s); return s; }
        public void LabTiro(V3 a, V3 b)
        {
            var ua = MalhaMapa.ParaUnity(a.x, a.y, a.z); var ub = MalhaMapa.ParaUnity(b.x, b.y, b.z);
            var dir = ub - ua; dir.y = 0; dir.Normalize();
            fx.Tiro(ua, dir, Vector3.Cross(Vector3.up, dir), false, false);
            fx.Impacto(ub, ub - ua, false, false);
            foreach (var f in fumacas.Values) if (f.Cruza(a, b) && f.Furo(a, b, LabT) is Vector3 saiu) fx.TufoSmoke(saiu, ub - ua);
            labTiros.Add((ua, ub, LabT));
        }
        public void LabHE(V3 p)
        {
            foreach (var s in labSmokes) if ((new V2(s.origin.x, s.origin.y) - new V2(p.x, p.y)).Len < Match.HE_FURO + 60) s.Clear(p, Match.HE_FURO, 2.1f);
            foreach (var f in fumacas.Values) f.Explosao(p, LabT);
            var u = MalhaMapa.ParaUnity(p.x, p.y, p.z); fx.HE(u); Tremer(new V2(p.x, p.y), 0.9f); Empurrar(u, 2.4f, 6.5f);
        }
        /// <summary>Laboratório do LED: C4 plantada em p faltando "restante" segundos (o relógio da Sim fica parado, quem anda
        /// é o plantedAt) e desenha os efeitos (C4 + LED) como na partida.</summary>
        public void LabBomba(V2 p, float restante)
        {
            m.bombPlanted = true; m.bombPos = p; m.bombZ = NavGrid.ZAt(p); m.plantedAt = m.t - (Match.BOMB - restante);
            Efeitos();
        }
        public void LabC4(V3 p)
        {
            var u = MalhaMapa.ParaUnity(p.x, p.y, p.z); fx.ExplosaoC4(u); Tremer(new V2(p.x, p.y), 2.5f); Empurrar(u, 9f, 13f);
        }
        /// <summary>Vitrine: as 5 granadas em pé no chão, enfileiradas a partir de p (no tamanho do jogo) e um boneco do lado.</summary>
        public void LabGranadas(V2 p)
        {
            float z = NavGrid.ZAt(p); int i = 0;
            foreach (var (k, tr) in new[] { ("he", true), ("flash", true), ("smoke", true), ("molotov", false), ("molotov", true) })
            {
                var g = Granada(k, tr); g.transform.localScale = Vector3.one * ESC_GRANADA;
                g.transform.position = MalhaMapa.ParaUnity(p.x + i * 6, p.y, z) + Vector3.up * 0.08f; i++;
            }
            var b = Boneco3D.Criar("ct", transform); b.transform.position = MalhaMapa.ParaUnity(p.x - 8, p.y, z); b.Pose("mira_M4A4", 0, 0);
        }
        public FireArea LabFogo(V3 o) { var f = new FireArea(o); labFogos.Add(f); fx.Molotov(MalhaMapa.ParaUnity(o.x, o.y, o.z)); return f; }
        public void LabApagaFogo(FireArea f) { labFogos.Remove(f); fx.ApagouFogo(f); fx.Vapor(MalhaMapa.ParaUnity(f.origin.x, f.origin.y, f.origin.z), 1.1f); }
        public void LabItem(string gunId, V2 pos)
        {
            var k = new object(); labItens.Add(k);
            float z = NavGrid.ZAt(pos);
            var ac = NovoItem(Armas3D.Objeto(Boneco3D.Carregar("tr"), Armas3D.DoJogo(gunId), Boneco3D.matCorpo), MalhaMapa.ParaUnity(pos.x, pos.y, z) + Vector3.up * 0.05f, Vector3.zero, UnityEngine.Random.Range(0, 360f));
            ac.v = Vector3.zero; ac.w = Vector3.zero;
            armasChao[k] = ac;
        }
        /// <summary>Laboratório do apoio no chão: as 5 granadas, o kit e 4 armas caindo girando de meio metro numa fila.</summary>
        public void LabApoio(V2 p)
        {
            var itens = new List<(string nome, bool arma)> { ("smoke", false), ("flash", false), ("he", false), ("molotov", false), ("incendiaria", false), ("kit", false),
                                                            ("ak", true), ("awp", true), ("glock", true), ("deagle", true) };
            for (int i = 0; i < itens.Count; i++)
            {
                var q = new V2(p.x + (i - 4.5f) * (i >= 6 ? 9f : 6f) * 0.7071f + (i >= 6 ? 10f : 0f), p.y - (i - 4.5f) * (i >= 6 ? 9f : 6f) * 0.7071f + (i >= 6 ? 12f : 0f));
                var k = new object(); labItens.Add(k);
                var mao = MalhaMapa.ParaUnity(q.x, q.y, NavGrid.ZAt(q)) + Vector3.up * 0.5f;
                ArmaCaindo ac;
                if (itens[i].arma) ac = NovoItem(Armas3D.Objeto(Boneco3D.Carregar("tr"), Armas3D.DoJogo(itens[i].nome), Boneco3D.matCorpo), mao, Vector3.zero, i * 37f);
                else
                {
                    var gi = Granada(itens[i].nome, itens[i].nome != "incendiaria"); gi.transform.localScale = Vector3.one * ESC_GRANADA_CHAO;
                    ac = NovoItem(gi, mao, Vector3.zero, i * 37f); ac.meiaAltura = 0.03f * ESC_GRANADA_CHAO;
                }
                ac.v = new Vector3(0.3f, 0, 0.2f);
                armasChao[k] = ac; labNomes[k] = itens[i].nome;
            }
        }
        readonly Dictionary<object, string> labNomes = new Dictionary<object, string>();
        /// <summary>Folga entre o ponto mais baixo de cada item do laboratório e o chão (unidades do jogo; + = flutuando,
        /// - = afundado). Log "[Visual3D] APOIO".</summary>
        public void LabApoioMedir()
        {
            foreach (var kv in armasChao)
            {
                if (!labNomes.TryGetValue(kv.Key, out var nome)) continue;
                var ac = kv.Value; float mn = float.MaxValue;
                foreach (var mf in ac.go.GetComponentsInChildren<MeshFilter>())
                {
                    if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                    foreach (var v in mf.sharedMesh.vertices) mn = Mathf.Min(mn, mf.transform.TransformPoint(v).y);
                }
                foreach (var sr in ac.go.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var tmp = new Mesh(); sr.BakeMesh(tmp, false);
                    foreach (var v in tmp.vertices) mn = Mathf.Min(mn, sr.transform.TransformPoint(v).y);
                    Apagar(tmp);
                }
                float folga = (mn - Efeitos3D.Piso(ac.go.transform.position)) / MalhaMapa.ESC;
                var bb = new Bounds(ac.go.transform.position, Vector3.zero); int nr = 0;
                foreach (var rr in ac.go.GetComponentsInChildren<Renderer>()) { bb.Encapsulate(rr.bounds); nr++; }
                Debug.Log($"[Visual3D] APOIO {nome}: folga {folga:+0.00;-0.00} u (parada {ac.parada}, pts {ac.pts?.Length ?? 0}, {nr} renderers, tamanho {bb.size.x / MalhaMapa.ESC:0.0}x{bb.size.y / MalhaMapa.ESC:0.0}x{bb.size.z / MalhaMapa.ESC:0.0} u)");
            }
        }
        public void LabCamera(V2 alvo, float zoom) { camSuave = new Vector2(alvo.x, alvo.y); zoomSuave = zoom; camPronta = true; cen.alvoJogo = camSuave; cen.enquadrar = zoom; cen.Enquadrar(); }
        // laboratório de tiros: alvos parados levando um tiro cada (capacete, cabeça sem capacete, corpo, morte HS, morte corpo)
        class AlvoLab { public Boneco3D b; public bool hs, prot, mata, feito; public float t, ang, forca = 1f, velFrente; public Ragdoll3D rg; public string anim, nome; public Vector3 dir; }
        readonly List<AlvoLab> labAlvos = new List<AlvoLab>();
        /// <summary>Fila de 5 CTs (giro = pra onde olham), cada um leva um tiro pela frente em t0 + 0,25 s × i.</summary>
        public void LabTiros(V2 p, float giro, float t0 = 0.3f, string quais = "0,1,2,3,4")
        {
            var todos = new[] { (true, true, false), (true, false, false), (false, false, false), (true, false, true), (false, false, true) };
            var sel = quais.Split(','); var casos = new (bool, bool, bool)[sel.Length];
            for (int j = 0; j < sel.Length; j++) casos[j] = todos[int.Parse(sel[j])];
            for (int i = 0; i < casos.Length; i++)
            {
                float cx = (i - (casos.Length - 1) / 2f) * 16;
                var q = new V2(p.x + cx * 0.7071f, p.y - cx * 0.7071f);
                var b = Boneco3D.Criar("ct", transform);
                b.transform.SetPositionAndRotation(MalhaMapa.ParaUnity(q.x, q.y, NavGrid.ZAt(q)), Quaternion.Euler(0, giro, 0));
                b.Pose("mira_M4A4", 0, 0);
                labAlvos.Add(new AlvoLab { b = b, hs = casos[i].Item1, prot = casos[i].Item2, mata = casos[i].Item3, t = t0 + 0.25f * i, anim = "mira_M4A4" });
            }
        }
        void LabTirosPasso(float dt)
        {
            foreach (var al in labAlvos)
            {
                if (!al.feito && LabT >= al.t)
                {
                    al.feito = true;
                    var b = al.b; var dir = Quaternion.Euler(0, al.ang, 0) * -b.transform.forward;   // ang 0: a bala vem pela frente
                    al.dir = dir;
                    var alvoP = (al.hs ? b.Peca("cabeca") : b.Peca("tronco"))?.position ?? b.transform.position + Vector3.up * 0.4f;
                    var boca = alvoP - dir * 3f;
                    fx.Tiro(boca, dir, Vector3.Cross(Vector3.up, dir), false, false); fx.Rastro(boca, alvoP);
                    fx.Impacto(alvoP, dir, true, al.hs, al.prot);
                    if (al.mata) { b.Golpe(0); al.rg = new Ragdoll3D(b, dir, al.forca, 7 + labAlvos.IndexOf(al), al.hs, b.transform.forward * al.velFrente); }
                    else b.Golpe(0.35f + 0.6f, al.hs, 1);
                }
                if (al.rg != null) al.rg.Passo(dt);
                else if (al.velFrente > 0) { al.b.Pose(al.anim, LabT * 1.4f, dt); al.b.transform.position += al.b.transform.forward * al.velFrente * dt; }
                else al.b.Pose(al.anim, 0, dt);
            }
        }

        /// <summary>Laboratório das mortes: 8 CTs (giro = pra onde olham) morrendo juntos em t0 com força de fuzil, um caso
        /// cada: cabeça/peito × frente/costas/lado, cabeça correndo e peito agachado.</summary>
        public void LabMortes(V2 p, float giro, float t0 = 0.3f, string quais = "0,1,2,3,4,5,6,7")
        {
            var casos = new (string nome, bool hs, float ang, string anim, float vel)[] {
                ("HS frente", true, 0, "mira_M4A4", 0), ("HS costas", true, 180, "mira_M4A4", 0), ("HS lado", true, 90, "mira_M4A4", 0),
                ("peito frente", false, 0, "mira_M4A4", 0), ("peito costas", false, 180, "mira_M4A4", 0), ("peito lado", false, 90, "mira_M4A4", 0),
                ("HS correndo", true, 0, "andar_M4A4", 2.1f), ("peito agachado", false, 0, "agachar_M4A4", 0),
                ("peito atirando", false, 0, "tiro_M4A4", 0),   // 8: morre no meio do disparo (clarão na boca)
                ("peito correndo de verdade", false, 0, "andar_M4A4", 8.6f) };   // 9: na velocidade da Sim (215/s x 0,04): o que voava
            var sel = System.Array.ConvertAll(quais.Split(','), int.Parse);
            for (int k = 0; k < sel.Length; k++)
            {
                int i = sel[k];
                float cx = (k % 2 - 0.5f) * 34, cy = (k / 2 - (sel.Length > 2 ? 0.5f : 0)) * 34;
                var q = new V2(p.x + (cx + cy) * 0.7071f, p.y + (cy - cx) * 0.7071f);
                var b = Boneco3D.Criar("ct", transform);
                b.transform.SetPositionAndRotation(MalhaMapa.ParaUnity(q.x, q.y, NavGrid.ZAt(q)), Quaternion.Euler(0, giro, 0));
                b.Pose(casos[i].anim, 0, 0);
                labAlvos.Add(new AlvoLab { b = b, hs = casos[i].hs, mata = true, t = t0, ang = casos[i].ang, forca = 0.28f, velFrente = casos[i].vel, anim = casos[i].anim, nome = casos[i].nome });
            }
        }
        /// <summary>Resumo das mortes do laboratório: giro do tronco e pra onde a cabeça caiu (em relação à bala e à frente).</summary>
        public string LabMortesResumo()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var al in labAlvos)
            {
                if (al.rg == null) continue;
                var cp = al.rg.CabecaMenosPelve; cp.y = 0; cp.Normalize();
                var fr = al.b.transform.forward; fr.y = 0; fr.Normalize();
                sb.AppendLine($"[Visual3D] MORTE {al.nome}: giro {al.rg.Giro:0}° (líquido {al.rg.GiroLiq:+0;-0}°), cabeça caiu {Vector3.Dot(cp, al.dir):+0.00;-0.00} na direção da bala, {Vector3.Dot(cp, fr):+0.00;-0.00} pra frente do corpo{(al.rg.dormindo ? "" : " (ainda mexendo)")} [frente do tronco x do boneco {al.rg.FrenteConfere:+0.00;-0.00}], deslizou {al.rg.Deslize / MalhaMapa.ESC:0.0} da Sim");
            }
            return sb.ToString();
        }

        public void LabPasso(float dt)
        {
            LabT += dt;
            LabTirosPasso(dt);
            foreach (var s in labSmokes) s.Update(dt);
            foreach (var f in labFogos) f.Update(dt);
            // mesma regra da partida: chama acesa coberta pela smoke apaga o fogo inteiro
            foreach (var f in labFogos.ToList())
                for (int i = 0; i < f.Visible; i++)
                {
                    var c = f.cells[i]; var q = new V3(c.x * FireArea.C + 2, c.y * FireArea.C + 2, c.z + 6);
                    if (labSmokes.Exists(s => s.Sample(q, LabT) > 0.3f)) { LabApagaFogo(f); break; }
                }
            Fumacas(labSmokes, LabT);
            fx.Fogos(labFogos, dt); fx.Atualizar(dt);
            foreach (var ac in armasChao.Values) Fisica(ac, dt);
            rastro.Inicio();
            foreach (var (a, b, t0) in labTiros)
            {
                float idade = LabT - t0; if (idade > 0.09f) continue;
                var d = b - a; float cab = Mathf.Clamp01(idade / 0.05f), rabo = Mathf.Clamp01((idade - 0.025f) / 0.065f);
                var p0 = a + d * rabo; var p1 = a + d * cab; float l = Mathf.Max(0.02f, (p1 - p0).magnitude);
                rastro.Por((p0 + p1) * 0.5f, Quaternion.LookRotation(d), new Vector3(0.018f, 0.018f, l));
            }
            rastro.Fim();
            cen.alvoJogo = camSuave; cen.enquadrar = zoomSuave; cen.Enquadrar(); AplicarTremor(dt);
        }
    }
}
