using SecureGate.Contratos;
using SecureGate.Proteccion;
string positiva = "Scan starting...\r\nScan finished.\r\nScanning C:\\Prueba\\EICAR_Prueba.txt found 1 threats.\r\nThreat                  : Virus:DOS/EICAR_Test_File\r\nResources               : 1 total\r\n";
int hr = unchecked((int)0x80501003);
void Verificar(string nombre, string salida, int codigo, DeteccionAntivirus esperado, string errores = "", bool truncada = false)
{
 var r = InterpretadorDefender.Interpretar(codigo, salida, errores, truncada);
 if(r.Deteccion != esperado) throw new Exception(nombre + ": " + r.Detalle);
 if(esperado == DeteccionAntivirus.AmenazaDetectada && !r.Amenazas.Contains("Virus:DOS/EICAR_Test_File")) throw new Exception("Nombre de amenaza perdido.");
 Console.WriteLine("CORRECTO: " + nombre);
}
Verificar("reconoce la deteccion real con finales de linea Windows", positiva, hr, DeteccionAntivirus.AmenazaDetectada);
Verificar("reconoce finales de linea LF", positiva.Replace("\r\n", "\n"), hr, DeteccionAntivirus.AmenazaDetectada);
Verificar("reconoce amenaza en la ultima linea sin salto", "Threat : Virus:DOS/EICAR_Test_File", hr, DeteccionAntivirus.AmenazaDetectada);
Verificar("conserva escaneo sin detecciones", "Scanning C:\\Prueba\\cmd.exe found no threats.\r\n", 0, DeteccionAntivirus.SinDetecciones);
Verificar("codigo 2 sin evidencia no se convierte en amenaza", "Scan failed.\r\n", 2, DeteccionAntivirus.NoDeterminada);
Verificar("salida truncada no se acepta", positiva, hr, DeteccionAntivirus.NoDeterminada, truncada: true);
Verificar("salida contradictoria queda indeterminada", positiva + "Scanning C:\\Prueba\\EICAR_Prueba.txt found no threats.\r\n", hr, DeteccionAntivirus.NoDeterminada);
Verificar("nombre invalido no se acepta", "Threat : texto cualquiera\r\n", hr, DeteccionAntivirus.NoDeterminada);
