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
            Diseñado para negocios que manejan inventario con lotes y fechas de vencimiento.
          </LandingLead>
        </LandingSectionHead>
        <div className={`${shared.grid} ${styles.grid}`}>
          <article className={shared.card}>
            <h3 className={shared.cardTitle}>Droguerías</h3>
            <MutedText>
              Controla medicamentos por lote y vencimiento sin perder tiempo con planillas.
            </MutedText>
          </article>
          <article className={shared.card}>
            <h3 className={shared.cardTitle}>Minimercados</h3>
            <MutedText>
              Lleva el control diario de productos perecederos con menos errores en el cuadre.
            </MutedText>
          </article>
          <article className={shared.card}>
            <h3 className={shared.cardTitle}>Distribuidoras</h3>
            <MutedText>
              Centraliza lotes, cantidades y vencimientos para todo tu equipo en un solo lugar.
            </MutedText>
          </article>
        </div>
      </div>
    </section>
  )
}
