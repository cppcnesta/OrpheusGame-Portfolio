Shader "Orpheus/UI/WaterRipple"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _RectSize ("这张图在画布上的宽高（像素）", Vector) = (1920,1080,0,0)

        [Header(Water)]
        _WaterY ("水线高度（画布 y，画面中心为 0，向上为正）", Float) = -320
        _WaterDepth ("水线到画面底边的距离", Float) = 220
        _EdgeSoftness ("水线过渡（像素）", Float) = 24
        _Amplitude ("水面晃动幅度（像素）", Float) = 6
        _Frequency ("水面波纹密度", Float) = 2.2
        _Speed ("流动速度", Float) = 1.4
        _Shimmer ("波峰亮光", Range(0,1)) = 0.12

        [Header(Drops)]
        _RingAmplitude ("水滴涟漪推开的幅度（像素）", Float) = 10
        _RingSize ("涟漪扩散到多大（像素）", Float) = 260
        _RingPeriod ("一滴涟漪持续几秒", Float) = 3.2
        _RingSquash ("涟漪压扁程度（透视）", Float) = 3.5
        _RingShine ("涟漪亮光", Range(0,1)) = 0.18

        [Header(Haze)]
        _HazeAmplitude ("水汽波动幅度（像素，整幅画）", Float) = 0
        _HazeScale ("水汽波纹的大小（像素）", Float) = 140
        _HazeSpeed ("水汽流动速度", Float) = 0.8

        [Header(Sway)]
        _SwayAmplitude ("摆动幅度（像素，在 _SwayHeight 的高度）", Float) = 0
        _SwayBaseY ("摆动的根部（画布 y）", Float) = -540
        _SwayHeight ("根部往上多高摆到满幅", Float) = 220
        _SwaySpeed ("摆动速度", Float) = 1.1

        [Header(Mouse)]
        _MouseAmplitude ("鼠标波纹推开的幅度（像素）", Float) = 0
        _MouseSize ("鼠标波纹扩散到多大（像素）", Float) = 240
        _MouseLife ("鼠标波纹持续几秒", Float) = 1.6
        _MouseShine ("鼠标波纹亮光", Range(0,1)) = 0.12

        _Fade ("越往下越透明", Range(0,1)) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 worldPosition : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _RectSize;
            float _WaterY, _WaterDepth, _EdgeSoftness, _Amplitude, _Frequency, _Speed, _Shimmer;
            float _RingAmplitude, _RingSize, _RingPeriod, _RingSquash, _RingShine;
            float _HazeAmplitude, _HazeScale, _HazeSpeed;
            float _SwayAmplitude, _SwayBaseY, _SwayHeight, _SwaySpeed;
            float _MouseAmplitude, _MouseSize, _MouseLife, _MouseShine, _Fade;

            #define MOUSE_RIPPLES 12
            float4 _MouseRipples[MOUSE_RIPPLES];

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            float hash(float n) { return frac(sin(n * 127.1) * 43758.5453); }

            float Ring(float2 d, float radius, float width, float fade, out float2 dir)
            {
                float dist = length(d);
                dir = d / max(dist, 1);
                float front = (dist - radius) / width;
                return sin(front * 3.1) * exp(-front * front) * fade;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.worldPosition.xy;
                float below = _WaterY - p.y;
                float inWater = smoothstep(0, _EdgeSoftness, below);
                float depth = saturate(below / max(_WaterDepth, 1));
                float t = _Time.y * _Speed;
                float2 offset = 0;
                float shine = 0;

                // 水面多层正弦波扰动（越靠近底部幅度越大）
                float phase = _Frequency * sqrt(max(below, 0)) * 3.0;
                float wave = sin(phase - t * 2.0 + p.x * 0.004) * 0.6
                           + sin(phase * 1.7 + t * 1.3 - p.x * 0.011) * 0.3
                           + sin(phase * 2.9 - t * 0.7 + p.x * 0.023) * 0.1;
                offset += float2(wave, cos(phase * 0.8 - t * 1.6 + p.x * 0.006) * 0.35) * _Amplitude * lerp(0.35, 1, depth) * inWater;

                // 周期性随机水滴涟漪，纵向按 _RingSquash 做透视压扁
                for (int k = 0; k < 3; k++)
                {
                    float cycle = _Time.y / _RingPeriod + k / 3.0;
                    float id = floor(cycle) * 3 + k;
                    float age = frac(cycle);
                    float2 centre = float2(lerp(-820, 820, hash(id)), _WaterY - lerp(0.25, 0.85, hash(id + 17)) * _WaterDepth);
                    float2 dir;
                    float profile = Ring((p - centre) * float2(1, _RingSquash), age * _RingSize, 26, (1 - age) * (1 - age), dir);
                    offset += dir * profile * _RingAmplitude * inWater;
                    shine += max(profile, 0) * _RingShine * inWater;
                }

                // 全局水汽波动与前景芦苇摆动
                float ht = _Time.y * _HazeSpeed;
                float2 hp = p / max(_HazeScale, 1);
                float haze = sin(hp.y * 2.3 - ht * 2.1 + sin(hp.x * 0.9 + ht * 0.6) * 1.4)
                           + 0.5 * sin(hp.y * 4.1 + hp.x * 1.7 - ht * 1.3);
                float hazeWeight = lerp(0.35, 1, saturate((300 - p.y) / 700));
                offset += float2(haze, 0.35 * sin(hp.x * 2.0 + ht * 1.7 + hp.y)) * _HazeAmplitude * hazeWeight;

                float lift = saturate((p.y - _SwayBaseY) / max(_SwayHeight, 1));
                float gust = sin(_Time.y * _SwaySpeed + p.x * 0.004) + 0.4 * sin(_Time.y * _SwaySpeed * 2.3 + p.x * 0.011 + 1.3);
                offset.x += gust * _SwayAmplitude * lift * lift;

                // 由 TitleScreen 传入的鼠标轨迹波纹
                float squash = lerp(1.4, _RingSquash, inWater);
                for (int m = 0; m < MOUSE_RIPPLES; m++)
                {
                    float4 r = _MouseRipples[m];
                    float age = (_Time.y - r.z) / max(_MouseLife, 0.01);
                    if (r.w <= 0 || age < 0 || age > 1) continue;
                    float2 dir;
                    float profile = Ring((p - r.xy) * float2(1, squash), age * _MouseSize, 22, (1 - age) * (1 - age) * r.w, dir);
                    offset += dir * profile * _MouseAmplitude;
                    shine += max(profile, 0) * _MouseShine;
                }

                half4 color = (tex2D(_MainTex, i.uv - offset / _RectSize.xy) + _TextureSampleAdd) * i.color;
                color.rgb += (pow(saturate(wave), 6) * _Shimmer * inWater + shine) * color.a;
                color.a *= 1 - _Fade * depth * inWater;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
