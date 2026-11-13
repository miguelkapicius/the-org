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
    // PartidaAoVivo3D, parte: itens no chão com física (arma solta, granada largada, C4 caído) e ímã de pegar item.
    public partial class PartidaAoVivo3D
    {
        // ------------------------------------------------------------------ itens no chão com física
        // arma solta, granada largada pro colega e C4 caído: nascem na mão de quem soltou e caem de verdade — gravidade,
        // giro, quique com perda, rolam e param pelo atrito, batem em parede (o piso do lado mais alto que o item = parede,
        // a velocidade daquele eixo volta), e no fim deitam de lado. Explosão (HE, C4) empurra pra longe e faz voar.
        class ArmaCaindo { public GameObject go; public Vector3 p, v, w; public Quaternion r; public bool parada, soou; public float deitar, nasceu, meiaAltura = 0.035f, meioComp; public string gun;
            // granada do morto: a Sim rola ela a cada passo (ItemChao.pos/vel, dá pra pegar rolando, como no CS); o desenho
            // segue a posição atual suavizada e gira pela distância andada
            public bool rola, caixa; public int quiques; public Match.ItemChao item;
            // granada/kit no chão (dropada pro colega ou do morto): som do baque (Som3D.GranadaCaiu; kit fica mudo)
            public string nade;
            // amostra dos vértices do item (espaço dele, sem escala) e a escala: o chão apoia no ponto mais baixo DE VERDADE
            // depois do giro (antes era uma meia altura fixa: lata deitada flutuava, arma inclinada afundava)
            public Vector3[] pts; public Vector3 esc = Vector3.one; public Vector3 eixo = Vector3.right; public float giro;
            // caminho do item pro replay (só quando mexe): sem isso o replay mostrava a arma parada onde a física deixou no
            // fim do round (ex.: no ar, jogada pela explosão da C4)
            public readonly List<(float t, Vector3 p, Quaternion r)> fita = new List<(float, Vector3, Quaternion)>(); }
        readonly Dictionary<object, ArmaCaindo> armasChao = new Dictionary<object, ArmaCaindo>();
        const string C4_CAIDO = "c4 caído";

        ArmaCaindo NovoItem(GameObject go, Vector3 mao, Vector3 empurra, float yaw)
        {
            go.transform.SetParent(transform, false);
            // meio comprimento do item (a ponta do cano/coronha não pode entrar na parede)
            float meio = 0.05f;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) meio = Mathf.Max(meio, (r.bounds.max - go.transform.position).magnitude, (r.bounds.min - go.transform.position).magnitude);
            // vértices do item no espaço da raiz dele (até ~400, pulando), pro apoio no chão
            var pts = new List<Vector3>();
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>()) Amostra(pts, mf.sharedMesh, mf.transform, go.transform);
            // malha com esqueleto (o kit): os vértices na pose atual (BakeMesh), não os da pose de bind
            foreach (var sr in go.GetComponentsInChildren<SkinnedMeshRenderer>()) { var tmp = new Mesh(); sr.BakeMesh(tmp, false); Amostra(pts, tmp, sr.transform, go.transform); Apagar(tmp); }
            return new ArmaCaindo
            {
                go = go, nasceu = m.t, p = mao, meioComp = Mathf.Min(meio, 0.6f), pts = pts.ToArray(), esc = go.transform.localScale,
                v = empurra + new Vector3(UnityEngine.Random.Range(-0.4f, 0.4f), UnityEngine.Random.Range(1.3f, 2.0f), UnityEngine.Random.Range(-0.4f, 0.4f)),
                r = Quaternion.Euler(0, yaw, 0),
                w = new Vector3(UnityEngine.Random.Range(-400, 400), UnityEngine.Random.Range(-250, 250), UnityEngine.Random.Range(200, 500))
            };
        }

        static void Amostra(List<Vector3> pts, Mesh me, Transform t, Transform raiz)
        {
            if (me == null || !me.isReadable) return;
            var vs = me.vertices; int passo = Mathf.Max(1, vs.Length / 300);
            var esc = raiz.lossyScale; var inv = new Vector3(1 / Mathf.Max(1e-6f, esc.x), 1 / Mathf.Max(1e-6f, esc.y), 1 / Mathf.Max(1e-6f, esc.z));
            for (int i = 0; i < vs.Length; i += passo)
            {
                // ponto no mundo -> relativo à raiz, desfazendo giro e escala dela
                var w = t.TransformPoint(vs[i]);
                pts.Add(Vector3.Scale(Quaternion.Inverse(raiz.rotation) * (w - raiz.position), inv));
            }
        }

        /// <summary>Quanto o item desce abaixo do próprio centro com esse giro (o ponto mais baixo da malha): o centro fica
        /// a essa altura do chão. Sem malha legível, a meia altura antiga.</summary>
        static float Fundo(ArmaCaindo ac, Quaternion r)
        {
            if (ac.pts == null || ac.pts.Length == 0) return ac.meiaAltura;
            float mn = 0;
            foreach (var pt in ac.pts) { float y = (r * Vector3.Scale(ac.esc, pt)).y; if (y < mn) mn = y; }
            return -mn;
        }

        /// <summary>Item jogado da mão (o G do CS): arco até onde o Sim pôs; o 1º baque a 3/4 do caminho, o resto escorrega.</summary>
        static void Arco(ArmaCaindo ac, Vector3 mao, Vector3 alvo)
        {
            float vy = 1.6f, h = Mathf.Max(0.05f, mao.y - alvo.y), tq = (vy + Mathf.Sqrt(vy * vy + 24f * h)) / 12f;
            var hor = alvo - mao; hor.y = 0;
            ac.v = hor * (0.75f / tq) + Vector3.up * vy;
        }

        /// <summary>Mão de quem está perto do ponto (de onde o item sai) e pra onde ele empurra.</summary>
        (Vector3 mao, Vector3 empurra, float yaw) MaoDe(V2 pos, float z)
        {
            var dono = m.agents.Where(a => (a.pos - pos).Len < 40).OrderBy(a => (a.pos - pos).Len).FirstOrDefault();
            if (dono != null && bonecos.TryGetValue(dono.id, out var bd))
            {
                var e = mortes.TryGetValue(dono.id, out var mo) ? mo.dir * 1.1f : bd.transform.forward * 0.9f;
                return (bd.transform.position + Vector3.up * 0.45f, e, bd.transform.eulerAngles.y);
            }
            return (MalhaMapa.ParaUnity(pos.x, pos.y, z) + Vector3.up * 0.45f, Vector3.zero, UnityEngine.Random.Range(0, 360f));
        }

        /// <summary>Explosão: tudo que está solto no raio voa pra longe (mais perto = mais forte) girando.</summary>
        void Empurrar(Vector3 centro, float raio, float forca)
        {
            foreach (var ac in armasChao.Values)
            {
                var d = ac.p - centro; float dist = d.magnitude; if (dist > raio) continue;
                float k = 1 - dist / raio; d.y = 0; d = d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward;
                ac.v += (d + Vector3.up * 0.9f) * forca * k * UnityEngine.Random.Range(0.8f, 1.2f);
                ac.w += new Vector3(UnityEngine.Random.Range(-900, 900), UnityEngine.Random.Range(-600, 600), UnityEngine.Random.Range(-900, 900)) * k;
                ac.parada = false; ac.deitar = 0;
            }
        }

        void Quedas(float dt)
        {
            foreach (var d in m.drops)
            {
                if (armasChao.ContainsKey(d)) continue;
                string nome = Armas3D.DoJogo(d.gun); if (nome == null) continue;
                var (mao, emp, yaw) = MaoDe(d.pos, d.z);
                // dropou de propósito (pro colega na compra, ou trocou no chão): Drop.de diz quem; sem ele, o dono é quem está
                // mais perto (arma de quem morreu cai do corpo)
                var dono = d.de ?? m.agents.Where(a => (a.pos - d.pos).Len < 40).OrderBy(a => (a.pos - d.pos).Len).FirstOrDefault();
                bool jogou = dono != null && dono.alive && bonecos.ContainsKey(dono.id)
                             && (d.de != null || trocouEm.TryGetValue(dono.id, out float tt) && m.t - tt < 0.5f);
                if (jogou) { var tr = bonecos[dono.id].transform; mao = tr.position + Vector3.up * 0.5f + tr.forward * 0.15f; yaw = tr.eulerAngles.y; }
                var nova = NovoItem(Armas3D.Objeto(Boneco3D.Carregar(dono != null && !dono.IsT ? "ct" : "tr"), nome, Boneco3D.matCorpo), mao, emp, yaw);
                if (jogou) Arco(nova, mao, MalhaMapa.ParaUnity(d.pos.x, d.pos.y, d.z));
                nova.gun = d.gun;
                armasChao[d] = nova;
            }
            foreach (var it in m.itensChao)
            {
                if (armasChao.ContainsKey(it)) continue;
                var (mao, emp, yaw) = MaoDe(it.pos, it.z);
                // dropou pro colega: sai da mão de quem dropou num arco até onde o Sim pôs (o colega vai lá pegar)
                bool deMao = it.de != null && it.de.alive && bonecos.ContainsKey(it.de.id);
                if (deMao) { var tr = bonecos[it.de.id].transform; mao = tr.position + Vector3.up * 0.5f + tr.forward * 0.15f; yaw = tr.eulerAngles.y; }
                // granadas de quem morreu: saem do corpo (altura do peito) e se espalham num arco até onde o Sim pôs cada uma
                bool doMorto = !deMao && it.morto != null && bonecos.ContainsKey(it.morto.id);
                if (doMorto) { var tr = bonecos[it.morto.id].transform; mao = tr.position + Vector3.up * 0.35f; yaw = UnityEngine.Random.Range(0f, 360f); }
                var gi = Granada(it.nade, it.time == Team.T); gi.transform.localScale = Vector3.one * ESC_GRANADA_CHAO;
                var ac = NovoItem(gi, mao, emp * 0.6f, yaw); ac.meiaAltura = 0.03f * ESC_GRANADA_CHAO; ac.nade = it.nade;
                if (doMorto)   // sai do corpo (mao) e segue o rolo da Sim
                {
                    ac.rola = true; ac.item = it; ac.caixa = it.nade == "kit";
                    float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2); ac.eixo = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                }
                else if (deMao || doMorto) Arco(ac, mao, MalhaMapa.ParaUnity(it.pos.x, it.pos.y, it.z));
                armasChao[it] = ac;
            }
            if (m.bombDropped && !m.bombPlanted && !armasChao.ContainsKey(C4_CAIDO))
            {
                var (mao, emp, yaw) = MaoDe(m.droppedBomb, NavGrid.ZAt(m.droppedBomb));
                var ac = NovoItem(Armas3D.Prop(Boneco3D.Carregar("tr"), "plantar", 0.97f, Boneco3D.matCorpo), mao, emp * 0.6f, yaw); ac.meiaAltura = 0.045f;
                // largou de propósito (m.bombDe agora, vivo): sai da mão dele num arco até o lugar do Sim; morreu: cai do corpo
                var dono = m.bombDe != null && m.bombDe.alive && m.t - m.bombDeT < 1f && bonecos.ContainsKey(m.bombDe.id) ? m.bombDe : null;
                if (dono != null)
                {
                    var tr = bonecos[dono.id].transform; mao = tr.position + Vector3.up * 0.5f + tr.forward * 0.15f; ac.p = mao;
                    Arco(ac, mao, MalhaMapa.ParaUnity(m.droppedBomb.x, m.droppedBomb.y, NavGrid.ZAt(m.droppedBomb)));
                }
                armasChao[C4_CAIDO] = ac;
            }
            // pegaram do chão: o item é puxado como ímã pra quem pegou e some nele (usuário: sem pose de pegar, qualquer item)
            foreach (var k in armasChao.Keys.ToList())
            {
                bool existe = labItens.Contains(k) || (k is Match.Drop dr ? m.drops.Contains(dr) : k is Match.ItemChao ic ? m.itensChao.Contains(ic) : m.bombDropped && !m.bombPlanted);
                if (existe) continue;
                Agent quem = k is Match.Drop d2 ? d2.pegouPor : k is Match.ItemChao i2 ? i2.pegouPor
                           : k == (object)C4_CAIDO && !m.bombPlanted ? (m.c4PegouPor ?? m.agents.FirstOrDefault(a => a.alive && a.hasBomb)) : null;
                var go = armasChao[k].go; armasChao.Remove(k);
                if (quem != null && quem.alive && bonecos.ContainsKey(quem.id) && go != null)
                    imas.Add(new Ima { go = go, quem = quem.id, de = go.transform.position, esc = go.transform.localScale });
                else Apagar(go);
            }
            foreach (var ac in armasChao.Values) { Fisica(ac, dt); GravarItem(ac, dt); }
            Imas(dt);
        }

        // ímã da coleta: o item sai do chão, acelera até o peito de quem pegou, encolhe e some (0,2 s)
        class Ima { public GameObject go; public int quem; public Vector3 de, esc; public float t, espera; }
        readonly List<Ima> imas = new List<Ima>();
        const float IMA_DUR = 0.2f;
        void Imas(float dt)
        {
            for (int i = imas.Count - 1; i >= 0; i--)
            {
                var im = imas[i];
                if (im.go == null || !bonecos.TryGetValue(im.quem, out var b)) { if (im.go != null) Apagar(im.go); imas.RemoveAt(i); continue; }
                var alvo = b.transform.position + Vector3.up * 0.32f;
                // a regra da Sim pega a 12; o desenho espera no chão até ele chegar a ~6 ("quase por cima", usuário), no
                // máximo 0,6 s, e aí puxa
                var dh = alvo - im.de; dh.y = 0;
                if (im.t == 0 && im.espera < 0.6f && dh.magnitude > 6f * MalhaMapa.ESC) { im.espera += dt; continue; }
                im.t += dt;
                float k = Mathf.Clamp01(im.t / IMA_DUR);
                if (k >= 1) { Apagar(im.go); imas.RemoveAt(i); continue; }
                float e = k * k;   // começa devagar e acelera (puxado)
                var pos = Vector3.Lerp(im.de, alvo, e); pos.y += Mathf.Sin(k * Mathf.PI) * 0.08f;   // um arquinho
                im.go.transform.position = pos;
                im.go.transform.localScale = im.esc * Mathf.Lerp(1f, 0.25f, e);
            }
        }

        void GravarItem(ArmaCaindo ac, float dt)
        {
            if (ac.go == null) return;
            var tr = ac.go.transform; var f = ac.fita;
            if (f.Count > 0)
            {
                var u = f[f.Count - 1];
                if ((tr.position - u.p).sqrMagnitude < 1e-6f && Quaternion.Angle(tr.rotation, u.r) < 0.3f) return;   // parado: nada
                // voltou a mexer depois de parado (explosão, chute): segura a pose parada até o quadro anterior
                if (m.t - u.t > dt * 1.5f) f.Add((m.t - dt, u.p, u.r));
            }
            f.Add((m.t, tr.position, tr.rotation));
        }

        /// <summary>Replay: item onde estava no instante t (some antes de nascer).</summary>
        static void ItemNoReplay(ArmaCaindo ac, float t)
        {
            if (ac.go == null) return;
            bool v = ac.nasceu <= t; ac.go.SetActive(v);
            var f = ac.fita; if (!v || f.Count == 0) return;
            int i1 = f.FindIndex(e => e.t >= t); if (i1 < 0) i1 = f.Count - 1; int i0 = Mathf.Max(0, i1 - 1);
            var a = f[i0]; var b = f[i1]; float w = b.t > a.t ? Mathf.Clamp01((t - a.t) / (b.t - a.t)) : 1;
            ac.go.transform.SetPositionAndRotation(Vector3.Lerp(a.p, b.p, w), Quaternion.Slerp(a.r, b.r, w));
        }

        void Fisica(ArmaCaindo ac, float dt)
        {
            if (ac.rola)
            {
                // cai do corpo e rola junto da Sim: a posição dela anda a cada passo (32 Hz); aqui segue suavizada (sem degrau),
                // na altura do chão, girando pelo chão conforme anda
                var it = ac.item;
                var alvo = MalhaMapa.ParaUnity(it.pos.x, it.pos.y, NavGrid.ZAt(it.pos));
                float idade = m.t - it.t;
                if (idade < 0.18f) alvo.y = Mathf.Lerp(ac.p.y, alvo.y, Mathf.Clamp01(idade / 0.18f));   // a queda do peito até o chão
                var novo = Vector3.Lerp(ac.p, alvo, 1 - Mathf.Exp(-22f * dt));
                var passo = novo - ac.p; passo.y = 0; float dist = passo.magnitude;
                if (ac.caixa)
                {
                    // caixa (kit) não rola (usuário): uma virada na queda e depois só escorrega, chapada no chão
                    if (idade < 0.25f) ac.r = Quaternion.AngleAxis(180f * dt / 0.25f, ac.eixo) * ac.r;
                    else
                    {
                        var fr = Vector3.ProjectOnPlane(ac.r * Vector3.forward, Vector3.up); if (fr.sqrMagnitude < 1e-6f) fr = Vector3.forward;
                        var chapada = Quaternion.LookRotation(fr, Vector3.Dot(ac.r * Vector3.up, Vector3.up) >= 0 ? Vector3.up : Vector3.down);
                        ac.r = Quaternion.Slerp(ac.r, chapada, 1 - Mathf.Exp(-14f * dt));
                    }
                }
                else
                {
                    // cilindro (granada) deita de lado com o eixo atravessado no movimento e rola em volta dele
                    if (dist > 1e-5f)
                    {
                        var at = Vector3.Cross(Vector3.up, passo / dist);
                        if (Vector3.Dot(at, ac.eixo) < 0) at = -at;
                        ac.eixo = Vector3.Slerp(ac.eixo, at, 1 - Mathf.Exp(-10f * dt)).normalized;
                        float sinal = Mathf.Sign(Vector3.Dot(Vector3.Cross(Vector3.up, passo / dist), ac.eixo));
                        ac.giro += sinal * dist / Mathf.Max(0.01f, ac.meiaAltura) * Mathf.Rad2Deg;
                    }
                    var deitada = Quaternion.AngleAxis(ac.giro, ac.eixo) * Quaternion.FromToRotation(Vector3.up, ac.eixo);
                    ac.r = idade < 0.18f ? Quaternion.Slerp(ac.r, deitada, 1 - Mathf.Exp(-20f * dt)) : deitada;
                }
                novo.y = Mathf.Max(novo.y, Efeitos3D.Piso(novo) + Fundo(ac, ac.r));   // apoiado pelo ponto mais baixo (não flutua nem afunda)
                if (idade >= 0.18f)
                {
                    novo.y = Efeitos3D.Piso(novo) + Fundo(ac, ac.r);
                    // granada do morto chegou no chão (fim da queda do peito): o baque dela, uma vez
                    if (!ac.soou && ac.nade != null) { ac.soou = true; Som3D.GranadaCaiu(novo, ac.nade); }
                }
                ac.p = novo; ac.go.transform.SetPositionAndRotation(ac.p, ac.r);
                // parou na Sim e o desenho chegou: fica como está (deitada/chapada)
                if (it.vel.x == 0 && it.vel.y == 0 && (novo - alvo).sqrMagnitude < 1e-6f) { ac.rola = false; ac.parada = true; ac.v = Vector3.zero; ac.w = Vector3.zero; }
                return;
            }
            if (!ac.parada)
            {
                ac.v.y -= 12f * dt;
                var novo = ac.p + ac.v * dt;
                // parede: varre cada eixo do passo (de meio em meio voxel, parede fina não passa) -> bate e volta naquele eixo
                float alto = ac.p.y + 0.06f;
                var vx = Efeitos3D.Varre(ac.p, new Vector3(novo.x, ac.p.y, ac.p.z), alto);
                if (vx.x != novo.x) { ac.v.x = -ac.v.x * 0.4f; novo.x = vx.x; ac.w *= 0.7f; }
                var vz = Efeitos3D.Varre(new Vector3(novo.x, ac.p.y, ac.p.z), new Vector3(novo.x, ac.p.y, novo.z), alto);
                if (vz.z != novo.z) { ac.v.z = -ac.v.z * 0.4f; novo.z = vz.z; ac.w *= 0.7f; }
                ac.p = novo;
                ac.r = Quaternion.Euler(ac.w * dt) * ac.r;
                float piso = Efeitos3D.Piso(ac.p) + Fundo(ac, ac.r);
                if (ac.p.y < piso)
                {
                    ac.p.y = piso;
                    if (Mathf.Abs(ac.v.y) > 1.2f) fx.Passo(ac.p, Vector3.zero);   // poeirinha do baque
                    // 1º baque: o clipe já traz baque + 1 quique; cada quique seguinte da física soa mais baixo
                    if (ac.gun != null) { if (!ac.soou) { ac.soou = true; Som3D.ArmaCaiu(ac.p, ac.gun); } else Som3D.ArmaQuicou(ac.p, ac.gun, ++ac.quiques); }
                    else if (ac.nade != null) { Som3D.GranadaCaiu(ac.p, ac.nade, ac.soou ? ++ac.quiques : 0); ac.soou = true; }
                    ac.v = new Vector3(ac.v.x * 0.62f, -ac.v.y * 0.32f, ac.v.z * 0.62f); ac.w *= 0.55f;
                    // rolando/escorregando no chão até parar
                    if (Mathf.Abs(ac.v.y) < 0.5f)
                    {
                        ac.v.y = 0;
                        var h = new Vector3(ac.v.x, 0, ac.v.z);
                        if (h.magnitude < 0.08f) { ac.parada = true; ac.deitar = 0; }
                    }
                }
                if (ac.p.y <= piso + 0.001f && ac.v.y == 0)
                {   // atrito no chão (desliza e para) e o giro acompanha o rolamento
                    ac.v.x *= Mathf.Exp(-4f * dt); ac.v.z *= Mathf.Exp(-4f * dt); ac.w *= Mathf.Exp(-5f * dt);
                    // arma não rola (usuário): no chão ela já deita de lado e só gira chapada em volta da vertical enquanto
                    // escorrega (giro dos outros eixos fazia ela "rolar" como barril)
                    if (ac.gun != null)
                    {
                        ac.w = new Vector3(0, ac.w.y, 0);
                        var fw = ac.r * Vector3.forward; fw.y = 0; if (fw.sqrMagnitude < 1e-4f) fw = Vector3.forward;
                        ac.r = Quaternion.Slerp(ac.r, Quaternion.LookRotation(fw.normalized) * Quaternion.Euler(0, 0, 90), 1 - Mathf.Exp(-14f * dt));
                        ac.p.y = Efeitos3D.Piso(ac.p) + Fundo(ac, ac.r);
                    }
                    if (new Vector3(ac.v.x, 0, ac.v.z).magnitude < 0.08f) { ac.parada = true; ac.deitar = 0; }
                }
            }
            else if (ac.deitar < 1)
            {
                // assenta deitado de lado, mantendo pra onde o cano aponta
                ac.deitar = Mathf.Min(1, ac.deitar + dt * 5);
                var fw = ac.r * Vector3.forward; fw.y = 0; if (fw.sqrMagnitude < 1e-4f) fw = Vector3.forward;
                ac.r = Quaternion.Slerp(ac.r, Quaternion.LookRotation(fw.normalized) * Quaternion.Euler(0, 0, 90), ac.deitar);
                ac.p.y = Mathf.Lerp(ac.p.y, Efeitos3D.Piso(ac.p) + Fundo(ac, ac.r), ac.deitar);
            }
            ForaDaParede(ac);
            ac.go.transform.SetPositionAndRotation(ac.p, ac.r);
        }

        /// <summary>As pontas do item (cano e coronha, deitado no chão) não entram na parede: empurra pro lado livre.</summary>
        void ForaDaParede(ArmaCaindo ac)
        {
            var fw = ac.r * Vector3.forward; fw.y = 0; if (fw.sqrMagnitude < 1e-4f) return; fw.Normalize();
            var lado = new Vector3(fw.z, 0, -fw.x);
            float alto = ac.p.y - ac.meiaAltura + 0.04f, passo = MalhaMapa.ESC * 0.25f;
            foreach (var eixo in new[] { fw, -fw, lado, -lado })
            {
                float L = Mathf.Abs(Vector3.Dot(eixo, fw)) > 0.5f ? ac.meioComp : ac.meiaAltura * 2;
                for (int k = 0; k < 12 && Efeitos3D.Piso(ac.p + eixo * L) > alto && Efeitos3D.Piso(ac.p) <= alto; k++) ac.p -= eixo * passo;
            }
        }
    }
}
