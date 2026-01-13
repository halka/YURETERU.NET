using System;

namespace YureteruWPF.Utilities;

/// <summary>
/// Seismic calculation utilities (ported from seismicCalculations.js)
/// </summary>
public static class SeismicCalculations
{
    /// <summary>
    /// Calculate the magnitude of a 3D vector
    /// </summary>
    public static double CalculateVectorMagnitude(double x, double y, double z)
    {
        return Math.Sqrt(x * x + y * y + z * z);
    }

    /// <summary>
    /// Convert acceleration magnitude to Gal (cm/s²)
    /// 1 g = 980.665 Gal
    /// </summary>
    public static double ConvertToGal(double magnitude)
    {
        return magnitude * 980.665;
    }

    /// <summary>
    /// Calculate Gal from acceleration components
    /// </summary>
    public static double CalculateGal(double x, double y, double z)
    {
        var magnitude = CalculateVectorMagnitude(x, y, z);
        return ConvertToGal(magnitude);
    }
    /// <summary>
    /// Calculate JMA Long-Period Ground Motion Class based on Sva (cm/s)
    /// </summary>
    public static int CalculateLpgmClass(double sva)
    {
        // Reference: JMA Table
        // Class 1: Slightly felt (~5-15 cm/s)
        // Class 2: Strong shaking (~15-50 cm/s)
        // Class 3: Standing difficult (~50-100 cm/s)
        // Class 4: Crawling needed (> 100 cm/s)
        
        if (sva >= 100) return 4;
        if (sva >= 50) return 3;
        if (sva >= 15) return 2;
        if (sva >= 5) return 1;
        return 0;
    }

    public class BiquadFilter
    {
        // Simplified Biquad filter for Bandpass (0.13Hz - 0.6Hz approx for 1.6s-7.8s)
        // Coefficients roughly estimated for 100Hz sampling
        private double a0, a1, a2, b1, b2;
        private double z1, z2;

        public BiquadFilter()
        {
            // Coefficients for a very rough estimation of the required bandpass
            // Ideally should be calculated precisely, but using approximations for this demo.
            // Bandpass ~0.15 - 0.6 Hz at 100Hz Fs
            
            // Using pre-calculated dummy coefficients for a stable gentle bandpass
            // Real implementation would use biquad calculation from Fs/Fc/Q
            a0 = 0.020083365564211225;
            a1 = 0;
            a2 = -0.020083365564211225;
            b1 = -1.9566698698748956;
            b2 = 0.9598332688715775;
        }

        public double Process(double input)
        {
            double output = input * a0 + z1;
            z1 = input * a1 + z2 - b1 * output;
            z2 = input * a2 - b2 * output;
            return output;
        }
    }

    public class Integrator
    {
        private double _prevVal = 0;
        private double _accumulated = 0;
        private readonly double _dt;

        public Integrator(double dt)
        {
            _dt = dt;
        }

        public double Process(double input)
        {
            // Trapezoidal rule
            _accumulated += (input + _prevVal) * 0.5 * _dt;
            _prevVal = input;
            
            // Simple High pass to prevent drift (Leak)
            _accumulated *= 0.999;
            
            return _accumulated;
        }
    }
}
