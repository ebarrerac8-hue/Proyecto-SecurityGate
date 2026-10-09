using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using SecureGate.Servidor;

foreach (string ip in new[] { "10.0.0.2", "172.16.0.2", "172.31.0.2", "192.168.1.2" })
    _ = TransporteServidor.ValidarDireccion(ip);
foreach (string ip in new[] { "0.0.0.0", "127.0.0.1", "8.8.8.8", "172.32.0.2", "::1", "texto" })
{
    bool rechazo = false;
    try { TransporteServidor.ValidarDireccion(ip); } catch (InvalidOperationException) { rechazo = true; }
    if (!rechazo) throw new Exception("No se rechazó: " + ip);
}
Console.WriteLine("CORRECTO: admite IPv4 privadas y rechaza direcciones inválidas o públicas.");
using var clave = RSA.Create(2048);
var solicitud = new CertificateRequest("CN=PruebaSecureGate", clave, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
var san = new SubjectAlternativeNameBuilder(); san.AddDnsName("localhost"); san.AddIpAddress(IPAddress.Parse("192.168.1.2"));
solicitud.CertificateExtensions.Add(san.Build());
using var certificado = solicitud.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
TransporteServidor.ValidarCertificado(certificado, "192.168.1.2");
Console.WriteLine("CORRECTO: certificado con clave privada y SAN correspondientes.");
bool rechazado = false;
try { TransporteServidor.ValidarCertificado(certificado, "192.168.1.3"); }
catch (InvalidOperationException) { rechazado = true; }
if (!rechazado) throw new Exception("Se aceptó un certificado de otra dirección.");
Console.WriteLine("CORRECTO: rechaza certificado de otra dirección.");
using var publico = X509CertificateLoader.LoadCertificate(certificado.Export(X509ContentType.Cert));
rechazado = false;
try { TransporteServidor.ValidarCertificado(publico, "192.168.1.2"); }
catch (InvalidOperationException) { rechazado = true; }
if (!rechazado) throw new Exception("Se aceptó un certificado sin clave privada para el servidor.");
Console.WriteLine("CORRECTO: el certificado público no sirve como clave del servidor.");
Console.WriteLine("PRUEBAS DE TRANSPORTE COMPLETADAS; no se modificaron certificados de Windows ni reglas de red.");
