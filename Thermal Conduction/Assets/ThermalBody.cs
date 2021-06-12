using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThermalBody : MonoBehaviour
{
    public SpriteRenderer layer_prefab;
    public Shader temp_vis_shader;
    public float time_multiplier;

    public List<Layer> composition;
    public float outer_temp = 273; // replace with energy input from star or other

    private void Start()
    {
        DisplayBody();
    }
    float t;
    private void Update()
    {
        if(t >= 0.1f)
        {
            SphericalThermalConduction(t, time_multiplier / 1000f);
            DisplayBody();

            t = 0f;
        }
        t += Time.deltaTime;
    }

    void DisplayBody() // 2D visualizer
    {
        float radius = 0f;
        int i = 0;
        foreach (Layer layer in composition)
        {
            float temperature = layer.temperature;

            Material mat = new Material(temp_vis_shader);
            Color col = new Color(1f - (2f / (temperature * temperature * temperature)), 1f - (2f / (temperature * temperature)), 1f - (2f / (temperature)), 1f); // Wrong implimentation for blackbody radiation
            mat.color = col; 
            float layer_width = layer.GetThickness(radius);
            radius += layer_width;

            SpriteRenderer preview;
            if (i < transform.childCount)
            {
                preview = transform.GetChild(i).GetComponent<SpriteRenderer>();
            }
            else
            {
                preview = Instantiate(layer_prefab, Vector3.forward * (i * 0.2f) + Vector3.right * (i * 0.05f), Quaternion.identity);
            }

            preview.material = mat;
            preview.gameObject.transform.localScale = Vector3.one * radius;
            preview.transform.SetParent(gameObject.transform);

            i++;
        }
    }
    public void SphericalThermalConduction(float interval, float time_speed)
    {
        float[] masses = new float[composition.Capacity]; // Kg
        float[] densities = new float[composition.Capacity]; // Kg/m^3
        float[] radii = new float[composition.Capacity]; // m
        float[] thermal_conductivity = new float[composition.Capacity]; // Kelvin per unit time through unit area with a gradient of one degree per unit 
        float[] specific_heat = new float[composition.Capacity]; // Watts per K/cm^3 
        float[] temperatures = new float[composition.Capacity + 1]; // K

        float[] resistances = new float[composition.Capacity]; // ratio of heat tranfer per area to heat difference

        float vol = 0f;

        // This line conserves heat. Without it, heat will leak into nowhere.
        temperatures[composition.Capacity] = composition[composition.Capacity - 1].temperature;
        
        for (int i = 0; i < composition.Capacity; i++)
        {
            /// Setting layer variable arrays
            
            vol += composition[i].GetVolumeMassDensity(); // Iterate volume
            
            masses[i] = composition[i].mass; 
            densities[i] = composition[i].material.density;
            thermal_conductivity[i] = composition[i].material.thermal_conductivity;
            specific_heat[i] = composition[i].material.specific_heat;
            temperatures[i] = composition[i].temperature;
            // Find radius from volume
            radii[i] = Mathf.Pow((3f * vol) / (4f * Mathf.PI), 1f / 3f);
        }
        for (int i = 1; i < composition.Capacity; i++) // calculate the resistances per layer and set them
        {
            float radii_val = (1f / radii[i]) - (1f / radii[i - 1]);

            resistances[i - 1] = Mathf.Abs(radii_val) / (4 * Mathf.PI * thermal_conductivity[i]);
        }

        float[] new_temps = new float[composition.Capacity + 1]; // placeholder array for the temperature calculations
        for (int i = 0; i < composition.Capacity; i++)
        {
            new_temps[i] = temperatures[i];
        }
        for (int i = 0; i < composition.Capacity + 1; i++) // The real loop
        {
            if (i != 0 && temperatures[i - 1] - temperatures[i] > 0) // Thermal conduction inward
            {
                float difftemp = temperatures[i - 1] - temperatures[i]; // Inner temp - outer temp

                float dqdot = difftemp / resistances[i - 1]; // Heat transfer rate: Joules/sec

                float dtemp = dqdot * interval * time_speed / masses[i - 1] / specific_heat[i - 1]; // Delta T: change in temperature per unit of mass
                /* Safety fallback if timestep too small
                if (dtemp > difftemp / 2)
                    dtemp = difftemp / 2;
                */
                new_temps[i] += dtemp; // increment outer layer
                new_temps[i - 1] -= dtemp; // decrement inner layer
            }
            if (i != composition.Capacity && temperatures[i + 1] - temperatures[i] > 0) // Thermal conduction outward
            {
                float difftemp = temperatures[i + 1] - temperatures[i]; // Outer temp - inner temp
                float dqdot = difftemp / resistances[i]; // Joules/sec
                float dtemp = dqdot * interval * time_speed / masses[i] / specific_heat[i]; // Delta T: K/Gk^3
                /* Safety fallback
                if (dtemp > (difftemp / 2))
                {
                    dtemp = difftemp / 2;
                }*/
                new_temps[i] += dtemp;
                new_temps[i + 1] -= dtemp;
            }
        }
        for (int i = 0; i < composition.Capacity; i++) // Set the temperatures of each layer to the new calculated values.
        {
            if (new_temps[i] <= 0f) // Temperature cannot be less than 0.
            {
                new_temps[i] = 0.001f;
            }
            composition[i].temperature = new_temps[i];
        }
    }

    [System.Serializable]
    public class Layer
    {
        public LayerMaterial material;
        public float mass; // Kilograms
                           //public float thermal_energy; // Joules
        public float temperature; // Kelvin

        public float GetThickness(float inner_radius)  // Tested functional
        {
            // vol sphere = 3/4 * Pi * (r * r * r)

            float inner_volume = (3f / 4f) * (Mathf.PI * (inner_radius * inner_radius * inner_radius));

            float outer_volume = inner_volume + GetVolumeMassDensity();

            float outer_radius = Mathf.Pow((outer_volume) / ((3f / 4f) * Mathf.PI), 1f / 3f);

            float thickness = outer_radius - inner_radius;

            //Debug.Log("Inner radius: " + inner_radius + "\nOuter radius: " + outer_radius + "\nThickness:" + thickness);

            return thickness;
        }

        public float GetVolumeMassDensity()
        {
            float volume = mass / material.density;
            return volume;
        } // Cubic meters

        public float GetSurfaceArea(float inner_radius)
        {
            float inner_volume = (3f / 4f) * (Mathf.PI * (inner_radius * inner_radius * inner_radius));

            float outer_volume = inner_volume + GetVolumeMassDensity();

            float outer_radius = Mathf.Pow((3 * outer_volume) / (4f * Mathf.PI), 1f / 3f);

            float ret = 4f * Mathf.PI * (outer_radius * outer_radius);

            return ret;
        } // Square meters

        public Color BlackbodyColor()
        {
            return new Color();
        }

    }

    [CreateAssetMenu(menuName = "Astro/Material")]
    [System.Serializable]
    public class LayerMaterial : ScriptableObject
    {
        public float density; // Kilograms per cubic meter
        public float specific_heat; // Joules per Kelvin per kilogram
        public float thermal_conductivity; // Watts per Kilogram^-1   also  (conductivity)(surface area)/(thickness)
                                           // public float albedo; // [0, 1]. Will replace with spectrum information eventually

    }
}
