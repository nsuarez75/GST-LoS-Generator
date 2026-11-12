# Avisos de terceros



El código de GST LoS Generator se publica con licencia MIT (ver [LICENSE](LICENSE)). El ejecutable incluye los componentes

de terceros de esta lista, cada uno con su licencia. Las herramientas que solo se usan para compilar o probar

(SDK de .NET, Fody, xUnit…) no van dentro del ejecutable y no se listan.



| Componente | Licencia | Titular | Enlace |

|---|---|---|---|

| Avalonia (Avalonia, Avalonia.Desktop, Avalonia.Skia, Avalonia.Themes.Fluent y paquetes de plataforma) | MIT | The AvaloniaUI Project | https://github.com/AvaloniaUI/Avalonia |

| Inter (fuente, vía Avalonia.Fonts.Inter) | OFL-1.1 | The Inter Project Authors | https://github.com/rsms/inter |

| ANGLE (Avalonia.Angle.Windows.Natives) | BSD-3-Clause | The ANGLE Project Authors | https://chromium.googlesource.com/angle/angle |

| SkiaSharp y Skia nativo | MIT (Skia y sus dependencias: ver `licenses/SkiaSharp-THIRD-PARTY-NOTICES.txt`) | Microsoft Corporation | https://github.com/mono/SkiaSharp |

| HarfBuzzSharp y HarfBuzz nativo | MIT (HarfBuzz: ver `licenses/HarfBuzzSharp-THIRD-PARTY-NOTICES.txt`) | Microsoft Corporation | https://github.com/mono/SkiaSharp |

| MicroCom.Runtime | MIT | Nikita Tsukanov | https://github.com/kekekeks/MicroCom |

| Tmds.DBus.Protocol | MIT | Tom Deseyn | https://github.com/tmds/Tmds.DBus |

| Lucide (trazos de los iconos, copiados en `Controls/Icon.cs`) | ISC | Lucide Contributors; partes de Feather, Cole Bemis | https://lucide.dev |

| CommunityToolkit.Mvvm | MIT | .NET Foundation and Contributors | https://github.com/CommunityToolkit/dotnet |

| ClosedXML y ClosedXML.Parser | MIT | ClosedXML contributors | https://github.com/ClosedXML/ClosedXML |

| DocumentFormat.OpenXml (Open XML SDK) | MIT | Microsoft Corporation | https://github.com/dotnet/Open-XML-SDK |

| ExcelNumberFormat | MIT | andersnm | https://github.com/andersnm/ExcelNumberFormat |

| RBush | MIT | Turning Code, LLC (and others) | https://github.com/viceroypenguin/RBush |

| SixLabors.Fonts | Apache-2.0 | Six Labors | https://github.com/SixLabors/Fonts |

| Runtime de .NET y System.IO.Packaging (van dentro del ejecutable) | MIT | .NET Foundation and Contributors | https://github.com/dotnet/runtime |
| TIA-Connector (cliente y puente con TIA Portal, dentro del ejecutable) | MIT | Ignacio Suarez | https://github.com/nsuarez75/TIA-Connector |



## Textos de las licencias



- **MIT**: el texto es el de abajo, con el titular de cada componente en la línea de copyright.

- **ISC (Lucide)**: el texto completo va abajo.

- **BSD-3-Clause (ANGLE)**: [`licenses/ANGLE-BSD-3-Clause.txt`](licenses/ANGLE-BSD-3-Clause.txt).

- **Skia, HarfBuzz y sus dependencias nativas**: [`licenses/SkiaSharp-THIRD-PARTY-NOTICES.txt`](licenses/SkiaSharp-THIRD-PARTY-NOTICES.txt) y [`licenses/HarfBuzzSharp-THIRD-PARTY-NOTICES.txt`](licenses/HarfBuzzSharp-THIRD-PARTY-NOTICES.txt).

- **Apache-2.0**: [`licenses/Apache-2.0.txt`](licenses/Apache-2.0.txt).

- **OFL-1.1**: [`licenses/OFL-1.1.txt`](licenses/OFL-1.1.txt).



## Siemens TIA Portal Openness

El puente de TIA-Connector usa las librerías de TIA Portal Openness (`Siemens.Engineering.dll`), que son software
propietario de Siemens AG. **No se incluyen** ni en el repositorio ni en el ejecutable: se cargan de la instalación de
TIA Portal del propio equipo, y su uso se rige por la licencia de TIA Portal.

## Texto de la licencia MIT



```

MIT License

Copyright (c) <titular de cada componente, ver la tabla>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

```



## Licencia de Lucide (ISC)



```

ISC License

Copyright (c) for portions of Lucide are held by Cole Bemis 2013-2022 as part of Feather (MIT). All other copyright (c) for Lucide are held by Lucide Contributors 2022.

Permission to use, copy, modify, and/or distribute this software for any
purpose with or without fee is hereby granted, provided that the above
copyright notice and this permission notice appear in all copies.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.

```

