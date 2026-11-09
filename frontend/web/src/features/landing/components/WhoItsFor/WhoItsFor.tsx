import shared from '../../landing.module.css'
import { LandingLead, LandingSectionHead, LandingSectionTitle } from '../../landing-texts'
import { MutedText } from '../../../../styles/components/texts'
import styles from './WhoItsFor.module.css'

export function WhoItsFor() {
  return (
    <section id='audience' className={`${shared.section} ${shared.spaced}`}>
      <div className={shared.wrap}>
        <LandingSectionHead>
          <LandingSectionTitle>Para quién es Stockma</LandingSectionTitle>
          <LandingLead>
            Diseñado para negocios que manejan inventario con lotes, fechas de vencimiento y
            necesitan cero margen de error.
          </LandingLead>
        </LandingSectionHead>
        <div className={`${shared.grid} ${styles.grid}`}>
          <article className={shared.card}>
            <h3 className={shared.cardTitle}>Droguerías</h3>
            <MutedText>
              Evita sanciones y pérdidas por medicamentos vencidos en la parte trasera de la
              estantería. Controla cada lote con precisión milimétrica, agiliza la dispensación y
              dile adiós al estrés de revisar cajas una por una a medianoche.
            </MutedText>
          </article>

          <article className={shared.card}>
            <h3 className={shared.cardTitle}>Minimercados</h3>
            <MutedText>
              Gestiona productos de alta rotación y perecederos sin caer en faltantes de stock ni
              mermas ocultas. Mantén el inventario de lácteos, abarrotes y bebidas perfectamente
              sincronizado con tus ventas diarias.
            </MutedText>
          </article>

          <article className={shared.card}>
            <h3 className={shared.cardTitle}>Distribuidoras</h3>
            <MutedText>
              Centraliza la trazabilidad de grandes volúmenes de mercancía. Controla múltiples
              bodegas, lotes de entrada y salidas con un sistema rápido que le da visibilidad en
              tiempo real a todo tu equipo comercial y operativo.
            </MutedText>
          </article>

          <article className={shared.card}>
            <h3 className={shared.cardTitle}>Veterinarias</h3>
            <MutedText>
              Protege la salud animal y tu rentabilidad. Lleva el control estricto de vacunas,
              desparasitantes y fármacos delicados por fecha de caducidad, asegurando que ningún
              producto crítico expire sin que te des cuenta.
            </MutedText>
          </article>
        </div>
      </div>
    </section>
  )
}
