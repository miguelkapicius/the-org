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
    // PartidaAoVivo3D, parte: câmera da transmissão: diretor, mola, corte seco, tremor.
    public partial class PartidaAoVivo3D
    {
        // diretor da transmissão: segue o foco do Transmissao.Mirar (duelo > C4 > grupo do TR) com mola (acelera e freia);
        // quando o foco pula pra longe (outro duelo do outro lado do mapa) é corte seco com piscada preta, como um
        // diretor trocando de câmera, em vez de varrer o mapa. Zoom fecha no duelo e abre fora dele.
        Vector2 camSuave, camVel; bool camPronta; float zoomSuave, zoomVel, ultCorte = -9;
        const float CORTE_DIST = 420f;

        // diretor novo (Diretor, em Transmissao.cs): escolhe o protagonista e a caixa do quadro; aqui só a câmera:
        // dentro do plano segue com mola (mais rápida na troca de tiro), troca de plano longe = corte seco com piscada,
        // zoom pela caixa (nunca mais aberto que 2,1x nem mais fechado que o padrão fora da luta). -diretor_velho volta o antigo.
        readonly Diretor diretor = new Diretor();
        /// <summary>Quem o HUD mostra como observado: o escolhido no card ou o protagonista do diretor.</summary>
        public int Observado => seguir >= 0 ? seguir : camFixa.HasValue || Diretor.Velho ? -1 : diretor.prota;
        public Diretor DiretorAtual => diretor;
        void MoverCamera(float dt)
        {
            if (seguir >= 0 || camFixa.HasValue || Diretor.Velho) { MoverCameraVelho(dt); return; }
            var alvo = new Vector2(diretor.centro.x, diretor.centro.y);
            float w = diretor.x1 - diretor.x0, h = diretor.y1 - diretor.y0;
            float precisa = Mathf.Max((w + h * 0.5f) / 1.55f, (h + w * 0.4f) * 0.9f) * MalhaMapa.ESC;
            float z = Mathf.Clamp(precisa * 1.35f + 2.6f, diretor.luta ? zoom * 0.85f : zoom, zoom * 2.1f);
            if (!camPronta) { camSuave = alvo; zoomSuave = z; camPronta = true; }
            if (diretor.corte || (alvo - camSuave).magnitude > 700)
            {
                diretor.corte = false; camSuave = alvo; camVel = Vector2.zero; zoomSuave = z; zoomVel = 0; hud?.Corte();
            }
            camSuave = Vector2.SmoothDamp(camSuave, alvo, ref camVel, diretor.luta ? 0.35f : 0.6f, 520f, dt);
            zoomSuave = Mathf.SmoothDamp(zoomSuave, z, ref zoomVel, 0.8f, 100f, dt);
            cen.alvoJogo = camSuave; cen.enquadrar = zoomSuave; cen.Enquadrar();
            AplicarTremor(dt);
        }

        void MoverCameraVelho(float dt)
        {
            var alvo = new Vector2(foco.x, foco.y);   // o foco cru (o cam do Mirar já vem amortecido e nunca "pula")
            // troca de tiro perto do foco: enquadra OS DOIS LADOS (quem atira, o alvo e quem está no duelo junto) — o
            // centro vai pro meio deles e o zoom abre até caber todo mundo com folga (antes um dos lados ficava fora)
            float zDuelo = 0;
            if (seguir < 0)
            {
                float x0 = 1e9f, x1 = -1e9f, y0 = 1e9f, y1 = -1e9f; int n = 0;
                void Inclui(V2 p) { x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); y0 = Mathf.Min(y0, p.y); y1 = Mathf.Max(y1, p.y); n++; }
                foreach (var a in m.agents)
                {
                    if (!a.alive || a.brain.target == null || !a.brain.target.alive) continue;
                    if ((a.pos - foco).Len > 520 && (a.brain.target.pos - foco).Len > 520) continue;
                    Inclui(a.pos); Inclui(a.brain.target.pos);
                }
                if (n >= 2)
                {
                    var meio = new Vector2((x0 + x1) / 2, (y0 + y1) / 2);
                    alvo = Vector2.Lerp(alvo, meio, 0.75f);
                    // altura do quadro (unidades Unity) pra caber: largura conta 16:9, a profundidade encolhe na isométrica
                    float precisa = Mathf.Max((x1 - x0 + (y1 - y0) * 0.5f) / 1.55f, (y1 - y0 + (x1 - x0) * 0.4f) * 0.9f) * MalhaMapa.ESC;
                    zDuelo = Mathf.Clamp(precisa * 1.35f + 2.6f, zoom * 0.8f, zoom * 2.1f);
                }
            }
            if (!camPronta) { camSuave = alvo; zoomSuave = zoom; camPronta = true; }
            if ((alvo - camSuave).magnitude > CORTE_DIST && m.t - ultCorte > 2.5f)
            {
                camSuave = alvo; camVel = Vector2.zero; ultCorte = m.t; hud?.Corte();
            }
            camSuave = Vector2.SmoothDamp(camSuave, alvo, ref camVel, 0.55f, 420f, dt);
            bool duelo = m.agents.Any(a => a.alive && a.brain.target != null);
            float z = seguir >= 0 ? zoom * 0.85f : zDuelo > 0 ? zDuelo : duelo ? zoom * 0.95f : zoom * 1.12f;
            zoomSuave = Mathf.SmoothDamp(zoomSuave, z, ref zoomVel, 0.9f, 100f, dt);
            cen.alvoJogo = camSuave; cen.enquadrar = zoomSuave; cen.Enquadrar();
            AplicarTremor(dt);
        }

        // tremida de câmera: explosão perto do que a câmera mostra chacoalha forte, flash e AWP de leve
        float tremor, tremorT;
        void Tremer(V2 onde, float forca)
        {
            float d = (new Vector2(onde.x, onde.y) - camSuave).magnitude;
            tremor = Mathf.Max(tremor, forca * Mathf.Clamp01(1.2f - d / 700f));
        }
        void AplicarTremor(float dt)
        {
            tremorT += dt; tremor *= Mathf.Exp(-5.5f * dt);
            if (tremor < 0.002f) return;
            var o = new Vector3(Mathf.PerlinNoise(tremorT * 28, 1.3f) - 0.5f, Mathf.PerlinNoise(4.1f, tremorT * 28) - 0.5f, Mathf.PerlinNoise(tremorT * 24, 9.7f) - 0.5f);
            cen.cam.transform.position += o * tremor * 0.5f;
        }
    }
}
