// Dreamy Hellcore - alle GLSL-Quelltexte (OpenGL 3.3).
// Leitidee: anfangs traumhaft, dann schleichend unheimlicher bis zum psychologischen Terror (uA = Stimmung 0..1).
// Es ist dunkel. Licht gibt es nur gezielt: die Kante des Pfades, Tore/Checkpoints, das eigene kleine Licht,
// ein fernes Leuchten voraus - und seltene Blitze, die fuer einen Moment alles zeigen.
namespace DreamyHellcore;

static class Shaders
{
    // ------------------------------------------------------------------------------------------------
    // Szene: gemeinsamer Vertex-Shader (auch fuer den Tiefen-Vorpass, daher "invariant")
    // vertexTexCoord = (Wobble-Staerke, Leuchtstaerke bzw. Modus), vertexTangent = (u, v, Flaechenbreite, Flaechenhoehe) in Metern,
    // vertexColor.a = Material-ID / 255: 0 Struktur, 1 Plattform, 2 Abgrund, 4 reines Leuchten, 5 Raumhuelle, 6 Schattengestalt.
    public const string SceneVS = @"#version 330
in vec3 vertexPosition; in vec2 vertexTexCoord; in vec3 vertexNormal; in vec4 vertexColor; in vec4 vertexTangent;
uniform mat4 mvp; uniform mat4 matModel; uniform float uTime; uniform float uWeird;
out vec3 fPos; out vec3 fLoc; out vec3 fNrm; out vec4 fCol; out vec2 fUv; out vec4 fTan;
uniform float uShift;
invariant gl_Position;
void main(){
  vec3 p = vertexPosition;
  float w = vertexTexCoord.x * uWeird;
  p += w * vec3(sin(p.y*0.05+uTime*0.7), sin(p.z*0.04+uTime*0.5), sin(p.x*0.05+uTime*0.6));
  // Dekor verschiebt sich, waehrend der Spieler wegsieht (uShift zaehlt hoch); bei uShift = 0 unveraendert
  float sw = vertexTexCoord.x;
  p += step(0.001, sw)*min(sw*10.0, 4.0)*vec3(sin(sw*913.0+uShift*1.7)-sin(sw*913.0), 0.4*(sin(sw*517.0+uShift*2.3)-sin(sw*517.0)), sin(sw*271.0+uShift*3.1)-sin(sw*271.0));
  fPos = (matModel*vec4(p,1.0)).xyz; fLoc = p; fNrm = vertexNormal; fCol = vertexColor; fUv = vertexTexCoord; fTan = vertexTangent;
  gl_Position = mvp*vec4(p,1.0);
}";

    public const string DepthFS = @"#version 330
out vec4 finalColor;
in vec3 fPos; in vec4 fCol; uniform vec3 uCam; uniform float uGone;
void main(){ if (uGone > 0.5 && int(fCol.a*255.0+0.5) == 1 && uCam.z-fPos.z > 9.0) discard; finalColor = vec4(0.0); }";

    public const string SceneFS = @"#version 330
in vec3 fPos; in vec3 fLoc; in vec3 fNrm; in vec4 fCol; in vec2 fUv; in vec4 fTan;
uniform vec3 uCam; uniform vec3 uFog; uniform vec3 uAbyss; uniform vec3 uEmis; uniform vec3 uGlow; uniform vec3 uRes; uniform vec3 uLampC;
uniform float uFog0; uniform float uFog1; uniform float uDepth; uniform float uA; uniform float uDream; uniform float uTime;
uniform float uLampR; uniform float uBeat; uniform float uCell; uniform float uFlash; uniform float uDie; uniform float uGone; uniform float uEyes; uniform float uDark; uniform float uHeaven; uniform float uFront;
uniform vec4 uLP[8]; uniform vec3 uLC[8];
out vec4 finalColor;

float hash(vec3 p){ p = fract(p*0.3183099+0.1); p *= 17.0; return fract(p.x*p.y*p.z*(p.x+p.y+p.z)); }
float noise(vec3 x){
  vec3 i = floor(x), f = fract(x); f = f*f*(3.0-2.0*f);
  return mix(mix(mix(hash(i),hash(i+vec3(1,0,0)),f.x), mix(hash(i+vec3(0,1,0)),hash(i+vec3(1,1,0)),f.x), f.y),
             mix(mix(hash(i+vec3(0,0,1)),hash(i+vec3(1,0,1)),f.x), mix(hash(i+vec3(0,1,1)),hash(i+vec3(1,1,1)),f.x), f.y), f.z);
}
float fbm(vec3 p){ float a = 0.5, s = 0.0; for(int i=0;i<4;i++){ s += a*noise(p); p = p*2.03+vec3(7.1,3.3,1.7); a *= 0.5; } return s; }
vec3 pal(float t){ return 0.5+0.5*cos(6.2831*(t+vec3(0.0,0.33,0.67))); }
vec2 hash2(vec2 p){ return fract(sin(vec2(dot(p,vec2(127.1,311.7)), dot(p,vec2(269.5,183.3))))*43758.5453); }
// Voronoi-Zellgrenzen (F2-F1): eckiges Rissnetz wie in gesprungenem Beton
float voro(vec2 x){
  vec2 n = floor(x), f = fract(x); float d1 = 8.0, d2 = 8.0;
  for (int j=-1;j<=1;j++) for (int i=-1;i<=1;i++){
    vec2 g = vec2(float(i), float(j)); vec2 r = g+hash2(n+g)-f; float d = dot(r,r);
    if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) { d2 = d; }
  }
  return sqrt(d2)-sqrt(d1);
}
vec3 bump(vec3 n, vec3 p, float hgt){
  vec3 dpx = dFdx(p), dpy = dFdy(p); float dhx = dFdx(hgt), dhy = dFdy(hgt);
  vec3 r1 = cross(dpy, n), r2 = cross(n, dpx); float det = dot(dpx, r1);
  vec3 g = sign(det)*(dhx*r1 + dhy*r2);
  return normalize(abs(det)*n - g);
}
float sdCap(vec2 p, vec2 a, vec2 b, float r){ vec2 pa = p-a, ba = b-a; float h = clamp(dot(pa,ba)/dot(ba,ba), 0.0, 1.0); return length(pa-ba*h)-r; }

void main(){
  int mat = int(fCol.a*255.0+0.5);
  if (uGone > 0.5 && mat == 1 && uCam.z-fPos.z > 9.0) discard;   // der Weg hinter dem Spieler ist verschwunden
  vec3 alb = fCol.rgb;
  vec3 n0 = normalize(fNrm);
  vec3 toCam = uCam - fPos; float dist = length(toCam); vec3 V = toCam/dist;
  vec3 P = fLoc;
  float lod = 1.0/(1.0+dist*0.025);
  float macro = 2.5/uCell;
  vec3 Lg = normalize(vec3(0.0,0.10,1.0));
  vec3 col = vec3(0.0);
  float aoMask = 1.0;
  // Lichter hinter dem Spieler erloeschen (uDie waechst mit der Stimmung): die Dunkelheit folgt
  float behind = smoothstep(4.0, 16.0, uCam.z-fPos.z)*uDie;
  float flick = 1.0-0.85*uA*step(0.93, hash(vec3(floor(uTime*13.0), floor(P.z*0.2), 2.0)));

  if (mat == 2) {
    // ---- Abgrund: im Traum ein Sternenmeer, spaeter Glut, am Ende fast nichts mehr ----
    vec2 q = P.xz*0.32*macro; float t = uTime*0.05;
    float starAmt = 1.0-smoothstep(0.08, 0.42, uA);
    float lavaAmt = smoothstep(0.25, 0.55, uA)*(1.0-0.9*smoothstep(0.72, 1.0, uA));
    vec2 sg = P.xz*1.4*macro; vec2 si = floor(sg); vec2 sf = fract(sg)-0.5;
    float sh = hash(vec3(si, 3.0));
    float sd = length(sf-(hash2(si)-0.5)*0.7);
    float star = smoothstep(0.10, 0.0, sd)*step(0.5, sh)*(0.5+0.5*sin(uTime*(1.0+3.0*sh)+sh*40.0));
    vec3 starC = mix(vec3(0.7,0.8,1.0), vec3(1.0,0.82,0.7), fract(sh*7.0))*star*2.4;
    float neb = fbm(vec3(q*0.6+vec2(t*0.3,0.0), t*0.2));
    vec3 nebC = pal(neb*0.6+0.55)*neb*neb*0.10;
    float n1 = fbm(vec3(q + vec2(t,-t*0.6), t*0.7));
    float n2 = fbm(vec3(q*2.1 + n1*1.8 + vec2(-t*0.5,t), t));
    float crack = pow(1.0-abs(2.0*n2-1.0), 5.0);
    vec3 hot = mix(uAbyss, mix(vec3(1.0,0.55,0.18), vec3(1.0,0.14,0.04), smoothstep(0.5, 0.9, uA)), 0.6);
    col = (starC+nebC)*starAmt + (uAbyss*(0.05+0.4*n1*n1) + hot*crack*(2.0+1.8*uBeat))*lavaAmt;
    aoMask = 0.0;
    if (uHeaven > 0.01) {   // falscher Himmel: statt des Abgrunds ein langsam ziehendes Wolkenmeer
      float cl = fbm(vec3(q*1.3+vec2(t*0.4,t*0.1), t*0.3));
      col = mix(col, vec3(1.0,0.96,0.9)*(0.10+0.42*cl*cl)+vec3(1.0,0.7,0.4)*0.14*pow(cl,4.0), uHeaven);
    }
  } else if (mat == 4) {
    // ---- Tore und Checkpoint-Pfosten: die wenigen echten Lichtquellen ----
    col = alb*(1.7+0.3*sin(uTime*2.0+P.z*0.7+P.y))*(1.0-behind)*flick*(1.0-uDark);
    aoMask = 0.0;
  } else {
    // ---- Oberflaeche: im Traum glatt und hell, verfaellt mit der Stimmung (Koernung, Flecken, Risse, Adern) ----
    float grain = fbm(P*3.3)*lod;
    float hMac = fbm(P*macro*0.45);
    float stain = fbm(vec3(P.x*0.8, P.y*0.11, P.z*0.8)*max(macro,0.15)+3.0);
    float h = grain*0.55 + hMac*0.6;
    float decay = smoothstep(0.12, 0.6, uA);
    float crackAmt = mix(smoothstep(0.55, 0.75, noise(P*0.21*macro+5.0))*smoothstep(0.1, 0.3, uA), 1.0, smoothstep(0.3, 0.75, uA));
    float ridge = pow(1.0-abs(2.0*noise(P*1.1+1.6*grain)-1.0), 18.0)*lod;
    float ridgeM = pow(1.0-abs(2.0*noise(P*macro*0.8+1.5*hMac)-1.0), 10.0);
    vec2 cp = abs(n0.y) > 0.6 ? P.xz : (abs(n0.x) > 0.6 ? P.zy : P.xy);
    float crackV = (1.0-smoothstep(0.0, 0.045, voro(cp*0.6+0.45*grain)))*lod;
    float veins = mix(crackV, max(ridge, ridgeM), smoothstep(0.45, 0.8, uA))*crackAmt;
    vec3 N = bump(n0, fPos, (grain*0.035 + hMac*0.04)*(0.25+0.75*decay) - veins*0.03);

    float fmin = min(fTan.z, fTan.w);
    float ed = min(min(fTan.x, fTan.z-fTan.x), min(fTan.y, fTan.w-fTan.y));
    float ew0 = 0.025+0.004*fmin; float ew = max(ew0, 1.5*fwidth(ed));
    float edge = (1.0-smoothstep(0.0, ew, ed))*(ew0/ew);
    float glowFlag = fUv.y;
    vec3 em = vec3(0.0);
    float eyeMask = 0.0;

    if (mat == 1) {
      alb *= mix(0.92+0.12*hMac, (0.6+0.6*h)*(0.8+0.4*stain), decay);
      vec2 g = 0.5-abs(fract(fTan.xy*0.8)-0.5);
      float line = 1.0-smoothstep(0.0, 0.03+fwidth(fTan.x+fTan.y), min(g.x,g.y));
      alb *= 1.0-(0.12+0.3*decay)*line*lod;
      alb *= 1.0-0.7*crackV*crackAmt*decay;   // Plattformen: nur eckige Risse, keine Adern
      // die leuchtende Kante ist das einzige dauerhafte Licht des Pfades: sie zeigt, wohin man springen kann
      vec3 rimC = mix(vec3(0.95,0.92,1.0), uEmis, 0.5);
      em += rimC*edge*mix(0.60, 0.24, uA)*(1.0-0.6*behind)*(1.0-uDark);
      if (glowFlag > 0.01) {
        float rr = length(fTan.xy-0.5*fTan.zw);
        float ring = mix(0.55+0.45*sin(rr*3.0-uTime*1.6), 1.0, uA);
        em += uEmis*glowFlag*ring*1.1*(1.0-behind)*flick;
      }
    } else {
      alb *= mix(0.85+0.2*hMac, (0.5+0.8*h)*(0.65+0.7*stain), decay);
      alb *= 1.0-0.8*veins;
      alb += 0.05*edge*(1.0-uA);
      // Adern: nur im Herzschlag fuer einen Augenblick zu sehen
      em += mix(uAbyss*2.0, vec3(0.9,0.05,0.03), 0.5)*veins*smoothstep(0.4, 0.8, uA)*uBeat*uBeat*0.9;
      // Traum: Polarlicht an der Decke
      if (mat == 5 && n0.y < -0.5 && uDream > 0.001) {
        float au = fbm(vec3(P.x*0.35*macro, P.z*0.07*macro, uTime*0.04));
        em += pal(au*0.8+0.5+P.z*0.003)*pow(au, 3.0)*0.9*uDream;
      }
      // ---- Augen in den Waenden: leuchten nicht - man bemerkt sie erst, wenn Licht auf sie faellt ----
      float dens = 0.32*smoothstep(0.3, 0.9, uA);
      float S = min(fmin, uCell)*0.9;
      if (dens > 0.001 && S > 0.7) {
        vec2 uv = fTan.xy/S; vec2 id = floor(uv); vec2 f = fract(uv)-0.5;
        float seed = fTan.z*3.1+fTan.w*1.7+dot(n0,vec3(1.0,2.0,3.0));
        float h1 = hash(vec3(id, seed)), h2 = hash(vec3(id.yx, seed+3.7)), h3 = hash(vec3(id+11.0, seed+9.1));
        bool inside = (id.x+1.0)*S <= fTan.z+0.001 && (id.y+1.0)*S <= fTan.w+0.001;
        if (h1 < dens && inside) {
          f -= (vec2(h2,h3)-0.5)*0.16;
          float Rr = 0.34*(0.65+0.35*h3);
          vec2 p = f/Rr;
          float ph = fract(uTime*0.09*(0.6+h2)+h2*7.0);
          float open = max(smoothstep(0.0, 0.05, abs(ph-0.5)), uEyes);
          float ly = 0.62*open*sqrt(max(0.0, 1.0-p.x*p.x));
          float aa = fwidth(p.y)*1.5+0.02;
          float inEye = smoothstep(0.0, aa, ly-abs(p.y))*step(abs(p.x), 1.0);
          // Welt-Tangenten der Flaeche aus Bildschirmableitungen -> die Pupille folgt dem Spieler
          vec3 dpx = dFdx(fPos), dpy = dFdy(fPos); vec2 dux = dFdx(fTan.xy), duy = dFdy(fTan.xy);
          float det = dux.x*duy.y-duy.x*dux.y; det = abs(det) < 1e-9 ? 1e-9 : det;
          vec3 Tu = (dpx*duy.y-dpy*dux.y)/det, Tv = (dpy*dux.x-dpx*duy.x)/det;
          vec3 ec = fPos - Tu*(f.x*S) - Tv*(f.y*S);
          vec3 tc = normalize(uCam-ec);
          vec2 look = clamp(vec2(dot(tc,Tu), dot(tc,Tv)), -1.0, 1.0)*0.42;
          look.y *= 0.5;
          vec2 pp = p-look; float di = length(pp);
          float iris = smoothstep(0.46, 0.42, di);
          float slit = mix(1.0, 2.6, smoothstep(0.6, 1.0, uA));
          float pupil = smoothstep(0.20, 0.16, length(pp*vec2(slit,1.0)));
          float stri = noise(vec3(atan(pp.y,pp.x)*7.0, di*9.0, h1*50.0));
          vec3 irisC = mix(vec3(0.5,0.42,0.25), vec3(0.6,0.08,0.05), smoothstep(0.5,0.9,uA))*(0.45+0.9*stri)*(1.0-0.6*smoothstep(0.3,0.46,di));
          vec3 scl = vec3(0.62,0.56,0.5)*(0.55+0.45*smoothstep(0.0,0.4,ly-abs(p.y)));
          scl += vec3(0.5,0.02,0.02)*pow(1.0-abs(2.0*noise(vec3(p*5.0,h1*20.0))-1.0), 6.0)*0.7;
          vec3 ecol = mix(scl, irisC, iris); ecol = mix(ecol, vec3(0.0), pupil);
          float lidShadow = (1.0-smoothstep(0.0, 0.22, abs(ly-abs(p.y))))*step(abs(p.x), 1.15);
          alb *= 1.0-0.7*lidShadow*(1.0-inEye);
          alb = mix(alb, ecol, inEye);
          em *= 1.0-inEye;
          em += inEye*vec3(0.9,0.85,0.7)*iris*(1.0-pupil)*(0.035+1.3*uEyes);       // kaum sichtbarer Schimmer im Dunkeln
          eyeMask = inEye;
          N = normalize(mix(N, n0, inEye));
        }
      }
    }

    // ---- Beleuchtung: fast kein Umgebungslicht ----
    float ndv = max(dot(N,V), 0.0);
    float fres = pow(1.0-ndv, 4.0);
    float gloss = (mat == 1 ? 0.5 : 0.25) + 0.6*eyeMask;
    vec3 lit = mix(uAbyss*0.5, uFog*1.2, N.y*0.5+0.5);
    vec3 spec = vec3(0.0);
    // das eigene Licht: klein, weich, im Terror unzuverlaessig
    float la = clamp(1.0-dist/uLampR, 0.0, 1.0); la *= la;
    lit += uLampC*la*(0.3+0.7*ndv);
    spec += uLampC*la*pow(ndv, 48.0)*gloss;
    // fernes Leuchten voraus: Gegenlicht und ein Glanzstreifen auf den Oberseiten
    lit += uGlow*0.5*max(dot(N,Lg), 0.0);
    spec += uGlow*1.8*pow(max(dot(N, normalize(Lg+V)), 0.0), 90.0)*gloss;
    // Glut von unten
    lit += uAbyss*(0.5+0.5*uBeat)*max(-N.y, 0.0);
    // Punktlichter: nur Tore und Checkpoints (sichere Inseln)
    for (int i=0;i<8;i++){
      vec3 d = uLP[i].xyz-fPos; float dl = length(d);
      float at = clamp(1.0-dl/uLP[i].w, 0.0, 1.0); at *= at;
      vec3 L = d/max(dl, 0.001);
      lit += uLC[i]*at*max(dot(N,L), 0.0)*1.3;
      spec += uLC[i]*at*pow(max(dot(N, normalize(L+V)), 0.0), 50.0)*gloss*1.4;
    }
    // Blitz: fuer einen Moment ist der ganze Raum zu sehen
    lit += vec3(0.75,0.82,1.0)*uFlash*1.5*(0.35+0.65*max(dot(N, normalize(vec3(0.3,0.8,-0.5))), 0.0));
    lit += vec3(1.0,0.95,0.84)*uHeaven*(0.08+0.10*N.y);   // falscher Himmel: weiches, schattenloses Licht von ueberall
    col = alb*lit + spec + em;
    col += (uFog+uGlow*0.25)*fres*0.22;
  }

  // ---- Nebel: nur entlang der Raumachse (Seitenwaende bleiben sichtbar), leicht wabernd ----
  float fn = 0.8+0.4*noise(vec3(fPos.xy*0.05*macro, fPos.z*0.05*macro)+vec3(0.0, uTime*0.04, -uTime*0.07));
  float f = smoothstep(uFog0, uFog1, abs(fPos.z-uCam.z)*fn);
  float g = clamp((uCam.y-4.0-fPos.y)/(uDepth*0.9), 0.0, 1.0)*(mat == 2 ? 0.40 : 0.85);
  col = mix(col, uAbyss*0.30, g);
  float gl = max(dot(-V, Lg), 0.0);
  vec3 fogC = uFog + uGlow*(0.10*pow(gl,4.0) + 0.42*pow(gl,24.0) + 2.0*pow(gl,220.0)) + vec3(0.5,0.55,0.7)*uFlash*0.22;
  col = mix(col, fogC, f);
  // Escape-Raum Flucht: hinter der Front ist alles schwarz, an der Front ein gluehender Saum
  float frk = 1.0-smoothstep(uFront-1.0, uFront+1.5, fPos.z);
  col = mix(col, vec3(0.0), frk) + vec3(1.0,0.22,0.08)*(1.0-smoothstep(0.0, 0.9, abs(fPos.z-uFront)))*1.6;
  finalColor = vec4(col, (1.0-f)*aoMask);
}";

    // ------------------------------------------------------------------------------------------------
    // Schattengestalten: eigener, sehr billiger Shader (nur Distanzfeld + Nebel). Die Silhouette ist kein Quader,
    // sondern ein weich ausfransender, zu langer Koerper. fUv.y = Modus: 0 Verfolger, 1 nur im Augenwinkel, 2 nur im Blitz.
    public const string ShadeFS = @"#version 330
in vec3 fPos; in vec2 fUv; in vec4 fTan;
uniform vec3 uCam; uniform vec3 uFog; uniform vec3 uGlow; uniform vec3 uRes;
uniform float uFog0; uniform float uFog1; uniform float uA; uniform float uTime; uniform float uFlash; uniform float uHeaven;
out vec4 finalColor;
float hash(vec3 p){ p = fract(p*0.3183099+0.1); p *= 17.0; return fract(p.x*p.y*p.z*(p.x+p.y+p.z)); }
float noise(vec3 x){
  vec3 i = floor(x), f = fract(x); f = f*f*(3.0-2.0*f);
  return mix(mix(mix(hash(i),hash(i+vec3(1,0,0)),f.x), mix(hash(i+vec3(0,1,0)),hash(i+vec3(1,1,0)),f.x), f.y),
             mix(mix(hash(i+vec3(0,0,1)),hash(i+vec3(1,0,1)),f.x), mix(hash(i+vec3(0,1,1)),hash(i+vec3(1,1,1)),f.x), f.y), f.z);
}
float sdCap(vec2 p, vec2 a, vec2 b, float r){ vec2 pa = p-a, ba = b-a; float h = clamp(dot(pa,ba)/dot(ba,ba), 0.0, 1.0); return length(pa-ba*h)-r; }
void main(){
  float mode = fUv.y, vis = 1.0;
  vec2 sc = gl_FragCoord.xy/uRes.xy-0.5; sc.x *= uRes.x/uRes.y;
  if (mode > 1.5) vis = max(step(0.12, uFlash), step(0.5, uHeaven));                                                   // nur im Blitz
  else if (mode > 0.5) vis = max(smoothstep(0.42, 0.66, length(sc)), step(0.12, uFlash));    // nur im Augenwinkel
  if (vis <= hash(vec3(gl_FragCoord.xy, 1.0))) discard;
  vec2 uv = fTan.xy/fTan.zw;
  vec2 p = vec2((uv.x-0.5)*0.36, uv.y);
  float sway = sin(uTime*0.6+fPos.z)*0.004;
  p.x += sway*p.y*2.0 + sin(uTime*1.3+p.y*9.0)*0.0015;
  vec2 q = vec2(abs(p.x), p.y);
  float d = length((p-vec2(sway*1.5,0.93))*vec2(1.0,0.82))-0.036;       // Kopf
  d = min(d, sdCap(p, vec2(0.0,0.87), vec2(0.0,0.56), 0.034));           // Rumpf
  d = min(d, sdCap(q, vec2(0.0,0.845), vec2(0.062,0.835), 0.016));       // Schultern
  d = min(d, sdCap(q, vec2(0.066,0.83), vec2(0.078,0.60), 0.011));       // Oberarme
  d = min(d, sdCap(q, vec2(0.078,0.60), vec2(0.070,0.34), 0.009));       // Unterarme (zu lang)
  d = min(d, sdCap(q, vec2(0.020,0.56), vec2(0.028,0.02), 0.014));       // Beine
  float nz = noise(vec3(p*60.0, uTime*0.8));
  d += (nz-0.5)*0.006 + smoothstep(0.30, 0.0, p.y)*nz*0.03;              // franst aus, die Beine loesen sich auf
  if (uHeaven > 0.5) d = min(d, abs(length((p-vec2(sway*1.5,0.978))*vec2(1.0,3.0))-0.05)-0.004);   // Heiligenschein
  if (d > 0.0) discard;
  vec3 col = mix(vec3(0.0015), vec3(0.95,0.90,0.78), uHeaven);   // im falschen Himmel strahlend weiss und gesichtslos
  float e = smoothstep(0.0045, 0.002, length(vec2(abs(p.x-sway*1.5)-0.013, p.y-0.935)));
  col += vec3(1.0,0.92,0.8)*e*1.4*smoothstep(0.55, 0.85, uA)*step(mode, 0.5);   // der Verfolger hat zwei winzige helle Augen
  vec3 V = normalize(uCam-fPos);
  float f = smoothstep(uFog0, uFog1, abs(fPos.z-uCam.z));
  float gl = max(dot(-V, normalize(vec3(0.0,0.10,1.0))), 0.0);
  vec3 fogC = uFog + uGlow*(0.10*pow(gl,4.0) + 0.42*pow(gl,24.0) + 2.0*pow(gl,220.0)) + vec3(0.5,0.55,0.7)*uFlash*0.22;
  finalColor = vec4(mix(col, fogC, f), 0.0);
}";

    // ------------------------------------------------------------------------------------------------
    // Partikel: im Traum Gluehwuermchen, spaeter Asche, im Terror fast nichts
    public const string PartVS = @"#version 330
in vec3 vertexPosition; in vec2 vertexTexCoord; in vec4 vertexColor;
uniform mat4 mvp; uniform vec3 uCam; uniform vec3 uRight; uniform vec3 uUp; uniform vec3 uBox;
uniform float uTime; uniform float uRise; uniform float uSize;
out vec2 fUv; out vec4 fCol; out float fFade;
void main(){
  vec3 seed = vertexPosition;
  vec3 p = seed*uBox;
  p.y += uTime*uRise*(0.4+vertexColor.r);
  p.x += sin(uTime*0.31+seed.z*40.0)*1.2*(0.5+vertexColor.g);
  p.z += cos(uTime*0.23+seed.x*37.0)*1.2*(0.5+vertexColor.b);
  p = mod(p-uCam, uBox)-0.5*uBox+uCam;
  float d = length(p-uCam);
  fFade = smoothstep(0.4, 1.5, d)*(1.0-smoothstep(0.30*uBox.x, 0.48*uBox.x, d));
  p += (uRight*vertexTexCoord.x+uUp*vertexTexCoord.y)*uSize*(0.35+vertexColor.r);
  fUv = vertexTexCoord; fCol = vertexColor;
  gl_Position = mvp*vec4(p,1.0);
}";
    public const string PartFS = @"#version 330
in vec2 fUv; in vec4 fCol; in float fFade;
uniform vec3 uPartCol; uniform float uTime; uniform float uDensity;
out vec4 finalColor;
void main(){
  if (fCol.a > uDensity) discard;
  float d = length(fUv); float a = smoothstep(1.0, 0.0, d); a *= a;
  float tw = 0.5+0.5*sin(uTime*(0.8+2.5*fCol.g)+fCol.b*40.0);
  finalColor = vec4(uPartCol*a*tw*tw*fFade*(0.5+1.5*fCol.b), 0.0);
}";

    // ------------------------------------------------------------------------------------------------
    // Post-Processing. Alle Paesse berechnen uv aus gl_FragCoord (keine Flip-Probleme).
    public const string PostVS = @"#version 330
in vec3 vertexPosition; uniform mat4 mvp;
void main(){ gl_Position = mvp*vec4(vertexPosition,1.0); }";

    // Bloom runter: uRes.z = 1 -> erster Pass mit Helligkeitsschwelle
    public const string DownFS = @"#version 330
uniform sampler2D uSrc; uniform vec3 uRes;
out vec4 finalColor;
void main(){
  vec2 uv = gl_FragCoord.xy/uRes.xy; vec2 px = 1.0/uRes.xy;
  vec3 c = texture(uSrc, uv).rgb*4.0;
  c += texture(uSrc, uv+px*vec2(-1.0,-1.0)).rgb; c += texture(uSrc, uv+px*vec2(1.0,-1.0)).rgb;
  c += texture(uSrc, uv+px*vec2(-1.0,1.0)).rgb;  c += texture(uSrc, uv+px*vec2(1.0,1.0)).rgb;
  c /= 8.0;
  if (uRes.z > 0.5) { float br = max(c.r, max(c.g, c.b)); c *= smoothstep(0.35, 1.3, br); c = min(c, vec3(24.0)); }
  finalColor = vec4(c, 1.0);
}";
    public const string UpFS = @"#version 330
uniform sampler2D uSrc; uniform vec3 uRes;
out vec4 finalColor;
void main(){
  vec2 uv = gl_FragCoord.xy/uRes.xy; vec2 px = 1.0/uRes.xy;
  vec3 c = texture(uSrc, uv+px*vec2(-1.0,0.0)).rgb + texture(uSrc, uv+px*vec2(1.0,0.0)).rgb
         + texture(uSrc, uv+px*vec2(0.0,-1.0)).rgb + texture(uSrc, uv+px*vec2(0.0,1.0)).rgb;
  c = c*0.125*2.0 + texture(uSrc, uv).rgb*0.5;
  finalColor = vec4(c*0.72, 1.0);
}";

    // Composite: SSAO, Lichtstrahlen, Bloom, Linse, Tonemapping.
    // uProx = Naehe des Verfolgers (0..1): Doppelbilder, Bildrisse, Farbsaeume.
    public const string CompFS = @"#version 330
uniform sampler2D uScene; uniform sampler2D uDepth; uniform sampler2D uBloom;
uniform vec3 uRes; uniform vec3 uSun;
uniform float uTime; uniform float uWeird; uniform float uA; uniform float uDream; uniform float uProx; uniform float uFlash;
uniform float uPause; uniform float uTanFov; uniform float uBeat; uniform float uExposure; uniform float uInvert; uniform float uGlitch; uniform float uSweep;
out vec4 finalColor;
float lin(float d){ float n = 0.08, f = 6000.0; return 2.0*n*f/(f+n-(d*2.0-1.0)*(f-n)); }
float hash12(vec2 p){ vec3 p3 = fract(vec3(p.xyx)*0.1031); p3 += dot(p3, p3.yzx+33.33); return fract((p3.x+p3.y)*p3.z); }
vec3 aces(vec3 x){ return clamp((x*(2.51*x+0.03))/(x*(2.43*x+0.59)+0.14), 0.0, 1.0); }
void main(){
  vec2 uv = gl_FragCoord.xy/uRes.xy; float asp = uRes.x/uRes.y; vec2 px = 1.0/uRes.xy;
  // Bildrisse nur, wenn etwas sehr nah ist; leichtes Schwimmen im Traum und im Terror
  float band = floor(uv.y*36.0), gt = floor(uTime*9.0);
  float gp = max(uProx, uGlitch); float tear = step(0.99-0.12*gp, hash12(vec2(band,gt)))*gp;
  uv.x += tear*0.04*(hash12(vec2(gt,band))-0.5);
  uv += (uWeird*0.0020+uDream*0.0007)*vec2(sin(uv.y*38.0+uTime*1.7), cos(uv.x*31.0+uTime*1.3));
  vec2 c = uv-0.5;
  vec2 uvd = 0.5+c*(1.0-(0.05+0.06*uProx)*dot(c,c));
  vec2 ca = c*(0.003+0.005*uA+0.014*uProx+0.003*uBeat*uA+0.03*uGlitch);
  vec4 s0 = texture(uScene, uvd);
  vec3 col = vec3(texture(uScene, uvd+ca).r, s0.g, texture(uScene, uvd-ca).b);
  // SSAO aus der Tiefe, mit Ebenenkorrektur (keine Selbstverschattung schraeger Flaechen)
  float d0 = lin(texture(uDepth, uvd).r);
  float gx = lin(texture(uDepth, uvd+vec2(px.x,0.0)).r)-d0, gxm = d0-lin(texture(uDepth, uvd-vec2(px.x,0.0)).r);
  float gy = lin(texture(uDepth, uvd+vec2(0.0,px.y)).r)-d0, gym = d0-lin(texture(uDepth, uvd-vec2(0.0,px.y)).r);
  vec2 slope = vec2(abs(gx) < abs(gxm) ? gx : gxm, abs(gy) < abs(gym) ? gy : gym);
  float wr = 0.55+0.02*d0;
  float rad = clamp(wr/(d0*uTanFov*2.0), 0.003, 0.06);
  float ang = hash12(gl_FragCoord.xy)*6.2831, occ = 0.0;
  for (int i=0;i<12;i++){
    float fi = (float(i)+0.5)/12.0; float a2 = ang+float(i)*2.39996;
    vec2 o = vec2(cos(a2), sin(a2))*rad*sqrt(fi); o.x /= asp;
    float ds = lin(texture(uDepth, uvd+o).r);
    float diff = d0+dot(o/px, slope)-ds;
    occ += smoothstep(0.03*wr, 0.5*wr, diff)*(1.0-smoothstep(1.2*wr, 3.0*wr, diff));
  }
  float ao = 1.0-clamp(occ/12.0*1.8, 0.0, 1.0);
  col *= mix(1.0, ao, s0.a*0.9);
  // Doppelbild, wenn der Verfolger nah ist
  col = mix(col, texture(uScene, uvd+vec2(0.018,0.006)*uProx).rgb, 0.4*uProx);
  // Lichtstrahlen vom fernen Leuchten
  vec3 rays = vec3(0.0);
  if (uSun.z > 0.001) {
    vec2 dlt = (uSun.xy-uv)/20.0; vec2 p = uv+dlt*hash12(gl_FragCoord.xy+fract(uTime)*71.0); float w = 1.0;
    for (int i=0;i<20;i++){ rays += texture(uBloom, p).rgb*w; p += dlt; w *= 0.92; }
    rays *= uSun.z/20.0;
  }
  col += rays*(0.28+0.30*uDream);
  // Bloom; im Traum zusaetzlich eine weiche Ueberstrahlung des ganzen Bildes
  vec3 bl = texture(uBloom, uvd).rgb;
  col += bl*(0.30+0.22*uDream);
  col = mix(col, bl*1.4, 0.04+0.12*uDream);
  vec3 st = vec3(0.0);
  for (int i=-4;i<=4;i++){ if (i != 0) st += texture(uBloom, uvd+vec2(float(i)*0.028, 0.0)).rgb/(1.0+abs(float(i))); }
  col += st*vec3(0.45,0.6,1.0)*0.05;
  col += vec3(0.6,0.65,0.8)*uFlash*0.04;
  // Belichtung, Tonemapping, Farbkorrektur: mit der Stimmung entweicht die Farbe
  col = aces(col*uExposure);
  col = pow(col, vec3(1.0/2.2));
  float l = dot(col, vec3(0.299,0.587,0.114));
  col = mix(vec3(l), col, mix(1.15, 0.6, smoothstep(0.6, 1.0, uA)));
  col = mix(col, col*col*(3.0-2.0*col), 0.35);
  col = mix(col, vec3(l)*0.45, 0.8*uPause);
  col = mix(col, 1.0-col, uInvert);                                  // Negativ-Flackern
  col *= 1.0-0.93*smoothstep(0.30, 0.0, abs(uv.x-uSweep));         // ein Schatten zieht durchs Bild
  finalColor = vec4(col, 1.0);
}";

    // Letzter Pass: FXAA, Filmkorn, Vignette (zieht sich mit der Stimmung zu), Einblenden
    public const string FinalFS = @"#version 330
uniform sampler2D uSrc; uniform vec3 uRes; uniform vec3 uSrcRes;
uniform float uTime; uniform float uFade; uniform float uA; uniform float uProx;
out vec4 finalColor;
float hash12(vec2 p){ vec3 p3 = fract(vec3(p.xyx)*0.1031); p3 += dot(p3, p3.yzx+33.33); return fract((p3.x+p3.y)*p3.z); }
vec3 fxaa(vec2 uv, vec2 rcp){
  vec3 nw = texture(uSrc, uv+vec2(-1.0,-1.0)*rcp).rgb, ne = texture(uSrc, uv+vec2(1.0,-1.0)*rcp).rgb;
  vec3 sw = texture(uSrc, uv+vec2(-1.0,1.0)*rcp).rgb,  se = texture(uSrc, uv+vec2(1.0,1.0)*rcp).rgb;
  vec3 m = texture(uSrc, uv).rgb; vec3 lu = vec3(0.299,0.587,0.114);
  float lnw = dot(nw,lu), lne = dot(ne,lu), lsw = dot(sw,lu), lse = dot(se,lu), lm = dot(m,lu);
  float lmin = min(lm, min(min(lnw,lne), min(lsw,lse))), lmax = max(lm, max(max(lnw,lne), max(lsw,lse)));
  vec2 dir = vec2(-((lnw+lne)-(lsw+lse)), (lnw+lsw)-(lne+lse));
  float red = max((lnw+lne+lsw+lse)*0.03125, 0.0078125);
  float rdm = 1.0/(min(abs(dir.x), abs(dir.y))+red);
  dir = clamp(dir*rdm, vec2(-8.0), vec2(8.0))*rcp;
  vec3 a = 0.5*(texture(uSrc, uv+dir*(1.0/3.0-0.5)).rgb+texture(uSrc, uv+dir*(2.0/3.0-0.5)).rgb);
  vec3 b = a*0.5+0.25*(texture(uSrc, uv-dir*0.5).rgb+texture(uSrc, uv+dir*0.5).rgb);
  float lb = dot(b,lu);
  return (lb < lmin || lb > lmax) ? a : b;
}
void main(){
  vec2 uv = gl_FragCoord.xy/uRes.xy;
  vec3 col = fxaa(uv, 1.0/uSrcRes.xy);
  float gr = hash12(gl_FragCoord.xy+fract(uTime)*vec2(113.0,71.0))-0.5;
  col += gr*(0.03+0.04*uA)*(1.0-0.6*dot(col,vec3(0.333)));
  vec2 q = uv-0.5;
  col *= clamp(1.0-dot(q,q)*(1.1+0.9*uA+1.6*uProx), 0.0, 1.0);
  finalColor = vec4(col*uFade, 1.0);
}";
}
