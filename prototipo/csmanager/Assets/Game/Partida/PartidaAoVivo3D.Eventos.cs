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
    // PartidaAoVivo3D, parte: eventos da simulação viram efeito e som (tiro, impacto, HE, flash, molotov, passos).
    public partial class PartidaAoVivo3D
    {
        Efeitos3D fx;
        readonly HashSet<Match.ShotFx> tirosVistos = new HashSet<Match.ShotFx>();
        readonly HashSet<FireArea> fogosVistos = new HashSet<FireArea>();
        readonly List<FireArea> fogosAcesos = new List<FireArea>();
        readonly List<Vector3> centrosFogo = new List<Vector3>(); bool c4PlantadoVisto;
        float c4PlantouEm = -1, c4ExplodiuEm = -1;   // pro replay: a C4 plantada só aparece no chão depois do plant (e some na explosão)
        readonly Dictionary<FireArea, float> idadeFogo = new Dictionary<FireArea, float>(); int vaporesVistos;
        float ultHE = -1, ultFlash = -1; bool c4Explodiu;
        readonly Dictionary<int, int> passoVisto = new Dictionary<int, int>();

        /// <summary>O que aconteceu na simulação desde o último quadro vira partícula: tiro, impacto, HE, flash, molotov.</summary>
        void Eventos(float dt)
        {
            foreach (var s in m.shots)
            {
                if (!tirosVistos.Add(s)) continue;
                var de = MalhaMapa.ParaUnity(s.a.x, s.a.y, s.za); var ate = MalhaMapa.ParaUnity(s.b.x, s.b.y, s.zb);
                // quem atirou: o vivo do time mais perto da origem do tiro
                Agent quem = null; float melhor = 30;
                foreach (var a in m.agents) { float d = (a.pos - s.a).Len; if (a.team == s.team && d < melhor) { melhor = d; quem = a; } }
                var rec = new TiroRec { t = s.t, ate = ate, boca = de, hit = s.hit, hs = s.hs };
                if (quem != null && bonecos.TryGetValue(quem.id, out var b))
                {
                    var tr = b.transform;
                    var dir = (ate - de); dir.y = 0; dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : tr.forward;
                    var boca = tr.position + dir * (quem.gunId == "awp" ? 0.62f : WeaponStats.Pistola(quem.gunId) ? 0.36f : 0.5f) + Vector3.up * Mathf.Lerp(0.5f, 0.36f, Suave(quem.duck));
                    // o fuzil fica do lado direito do colete (SMG um pouco, pistola no meio); a AWP de luneta vai no olho
                    float lado = quem.gunId == "awp" && quem.scoped ? 0.06f : WeaponStats.Pistola(quem.gunId) ? 0.02f : quem.gunId == "mp9" || quem.gunId == "mac10" ? 0.065f : 0.17f;
                    boca += Vector3.Cross(Vector3.up, dir) * lado;
                    // a ponta do cano de verdade (a peça da arma na mão), quando ela está visível
                    if (b.PontaDaArma(Arma(quem), dir, out var ponta)) boca = ponta;
                    fx.Tiro(boca, dir, Vector3.Cross(Vector3.up, dir), WeaponStats.Pistola(quem.gunId), quem.gunId == "awp");
                    Som3D.Tiro(boca, quem.gunId, quem.id); Som3D.Capsula(boca, quem.gunId); rec.gun = quem.gunId;
                    rec.temBoca = true; rec.boca = boca; rec.dir = dir; rec.pistola = WeaponStats.Pistola(quem.gunId); rec.awp = quem.gunId == "awp";
                    rec.quem = quem.id; b.Disparo(rec.awp ? 1f : rec.pistola ? 0.7f : 0.5f);   // o coice passa pro corpo
                }
                // quem levou: capacete (HS) / colete (tronco) abafam o sangue
                if (s.hit)
                {
                    Agent vit = null; float mv = 24;
                    foreach (var a in m.agents) { float d = (a.pos - s.b).Len; if (a.team != s.team && d < mv) { mv = d; vit = a; } }
                    rec.prot = vit != null && (s.hs ? vit.helmet : vit.armor > 0);
                    if (vit != null) ultAcerto[vit.id] = (s.hs, (ate - de).normalized, m.t);
                }
                if (rec.temBoca) fx.Rastro(rec.boca, ate);
                fx.Impacto(ate, ate - de, s.hit, s.hs, rec.prot);
                if (s.hit) Som3D.Impacto(ate, true, s.hs, rec.prot); else Som3D.Impacto(ate, false, false, false, MaterialEm(ate));
                Som3D.BalaPassando(rec.boca, ate, rec.gun);
                tirosGravados.Add(rec);
                var a3 = new V3(s.a.x, s.a.y, s.za); var b3 = new V3(s.b.x, s.b.y, s.zb);
                foreach (var f in fumacas.Values)
                    if (f.Cruza(a3, b3) && f.Furo(a3, b3, m.t) is Vector3 saiu) fx.TufoSmoke(saiu, ate - de);   // fumaça arrastada pela bala
                if (rec.awp) Tremer(s.a, 0.22f);
            }
            foreach (var h in m.hes) if (h.t > ultHE)
            {
                ultHE = h.t; var hp = MalhaMapa.ParaUnity(h.p.x, h.p.y, h.z);
                fx.HE(hp); Som3D.HE(hp); Tremer(h.p, 0.9f); Empurrar(hp, 2.4f, 6.5f);
                foreach (var f in fumacas.Values) f.Explosao(new V3(h.p.x, h.p.y, h.z), m.t);
            }
            if (m.exploded && !c4Explodiu)
            {
                c4Explodiu = true; c4ExplodiuEm = m.t;
                var bp = MalhaMapa.ParaUnity(m.bombPos.x, m.bombPos.y, m.bombZ);
                fx.ExplosaoC4(bp); Som3D.C4Explode(bp); Tremer(m.bombPos, 2.5f); Empurrar(bp, 9f, 13f);
                if (c4 != null) c4.SetActive(false);
            }
            foreach (var f in m.flashes) if (f.t > ultFlash) { ultFlash = f.t; var fp = MalhaMapa.ParaUnity(f.p.x, f.p.y, f.z); fx.Flash(fp); Som3D.Flash(fp); Tremer(f.p, 0.3f); }
            foreach (var f in m.fires) if (fogosVistos.Add(f))
            {
                var mp = MalhaMapa.ParaUnity(f.origin.x, f.origin.y, f.origin.z); fx.Molotov(mp);
                bool inc = false; float melhor = 9;
                foreach (var (pp, ehInc, tt) in molotovsSumiram) if (m.t - tt < 0.8f && (pp - mp).sqrMagnitude < melhor) { melhor = (pp - mp).sqrMagnitude; inc = ehInc; }
                // (a granada ainda pode estar desenhada neste quadro: o fogo nasce no mesmo passo em que ela some)
                foreach (var kv in props)
                    if (kv.Key is Match.Nade nn && (nn.l.kind == "molotov" || nn.l.kind == "incendiaria") && kv.Value != null && (kv.Value.transform.position - mp).sqrMagnitude < melhor)
                    { melhor = (kv.Value.transform.position - mp).sqrMagnitude; inc = nn.l.kind == "incendiaria" || (nn.by != null && !nn.by.IsT); }
                Som3D.Molotov(mp, inc);
            }
            molotovsSumiram.RemoveAll(x => m.t - x.t > 1f);
            // fogo aceso: um laço de som por molotov (lista vazia silencia)
            centrosFogo.Clear(); foreach (var f in m.fires) centrosFogo.Add(MalhaMapa.ParaUnity(f.origin.x, f.origin.y, f.origin.z));
            Som3D.Fogos(centrosFogo);
            // C4: plantou agora
            if (m.bombPlanted && !c4PlantadoVisto) { c4PlantadoVisto = true; c4PlantouEm = m.t; Som3D.C4Plantou(MalhaMapa.ParaUnity(m.bombPos.x, m.bombPos.y, m.bombZ)); Som3D.Anuncio("bomba_plantada"); }
            // fogo que sumiu ainda queimando = a smoke apagou: fumacinha de apagar em cada chama que estava acesa
            foreach (var f in fogosAcesos)
            {
                if (m.fires.Contains(f)) continue;
                var fp = MalhaMapa.ParaUnity(f.origin.x, f.origin.y, f.origin.z);
                if (f.age < FireArea.LIFE - 1.05f) { fx.ApagouFogo(f); Som3D.FogoApagou(fp); }   // a smoke apagou
                else Som3D.FogoFim(fp);                                                         // queimou até o fim
            }
            fogosAcesos.Clear(); fogosAcesos.AddRange(m.fires);
            // smoke apagou o fogo / molotov caiu na smoke e nem pegou (Match.fogoApagado): chiado de vapor
            if (m.fogoApagado.Count < vaporesVistos) vaporesVistos = 0;
            for (; vaporesVistos < m.fogoApagado.Count; vaporesVistos++)
            {
                var (vp, vz, _) = m.fogoApagado[vaporesVistos];
                fx.Vapor(MalhaMapa.ParaUnity(vp.x, vp.y, vz), 1.1f); Som3D.FogoApagou(MalhaMapa.ParaUnity(vp.x, vp.y, vz));
            }
            // poeirinha a cada passo correndo (o passo vira inteiro a cada meio ciclo)
            foreach (var a in m.agents)
            {
                if (!a.alive || a.Speed < a.MaxSpeed * 0.62f || a.noAr || !est.TryGetValue(a.id, out var e)) continue;
                int q = Mathf.FloorToInt(e.passo * 2);
                if (passoVisto.TryGetValue(a.id, out int q0) && q != q0 && bonecos.TryGetValue(a.id, out var b))
                {
                    var tr = b.transform; float lado = (q & 1) == 0 ? 0.07f : -0.07f;
                    fx.Passo(tr.position + tr.right * lado, new Vector3(a.vel.x, 0, -a.vel.y) * MalhaMapa.ESC);
                    Som3D.Passo(tr.position, Chao(a), !a.IsT);
                }
                passoVisto[a.id] = q;
            }
            fx.Fogos(m.fires, dt);
            fx.Atualizar(dt);
            Quedas(dt);
        }
    }
}
