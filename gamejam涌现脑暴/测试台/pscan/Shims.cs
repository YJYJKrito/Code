using System;

public struct Color {
  public float r, g, b, a;
  public Color(float r, float g, float b, float a) { this.r=r; this.g=g; this.b=b; this.a=a; }
  public static Color white => new Color(1,1,1,1);
  public static Color black => new Color(0,0,0,1);
}
public struct Color32 {
  public byte r, g, b, a;
  public Color32(byte r, byte g, byte b, byte a) { this.r=r; this.g=g; this.b=b; this.a=a; }
}
public static class Mathf {
  public static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
  public static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
  public static int Max(int a, int b) => a > b ? a : b;
  public static int Min(int a, int b) => a < b ? a : b;
  public static float Max(float a, float b) => a > b ? a : b;
  public static float Min(float a, float b) => a < b ? a : b;
  public static int Abs(int a) => Math.Abs(a);
  public static float Abs(float a) => Math.Abs(a);
  public static int RoundToInt(float f) => (int)Math.Round((double)f);
  public static int FloorToInt(float f) => (int)Math.Floor((double)f);
  public static int CeilToInt(float f) => (int)Math.Ceiling((double)f);
  public static float Sqrt(float f) => (float)Math.Sqrt(f);
  public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp(t, 0f, 1f);
}
