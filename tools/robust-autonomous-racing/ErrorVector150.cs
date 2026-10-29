namespace Robust150;
/// <summary>Prior measured prediction-error calibration only. No position, trajectory, action or switch time.</summary>
public sealed record ErrorVector150(string Model,float DX,float DY,float DVX,float DVY,float Heading,float Yaw,double Wheel,double Crank,string Source);
