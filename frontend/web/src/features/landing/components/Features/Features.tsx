import InventoryRoundedIcon from '@mui/icons-material/InventoryRounded'
import EventRoundedIcon from '@mui/icons-material/EventRounded'
import QrCodeScannerRoundedIcon from '@mui/icons-material/QrCodeScannerRounded'
import SyncAltRoundedIcon from '@mui/icons-material/SyncAltRounded'
import CreditCardRoundedIcon from '@mui/icons-material/CreditCardRounded'
import FileDownloadRoundedIcon from '@mui/icons-material/FileDownloadRounded'
import shared from '../../landing.module.css'
import { LandingLead, LandingSectionHead, LandingSectionTitle } from '../../landing-texts'
import { MutedText } from '../../../../styles/components/texts'
import styles from './Features.module.css'

export function Features() {
  return (
    <section id='features' className={`${shared.section} ${shared.spaced} ${shared.deep}`}>
      <div className={shared.wrap}>
        <LandingSectionHead>
          <LandingSectionTitle>Funcionalidades principales</LandingSectionTitle>
          <LandingLead>
            Tu stock centralizado, con menos trabajo manual y mayor control sobre cada uno de tus
            productos.
          </LandingLead>
        </LandingSectionHead>

        <div className={`${shared.grid} ${styles.grid}`}>
          <article className={`${shared.card} ${styles.card}`}>
            <div className={styles.iconWrapper}>
              <InventoryRoundedIcon />
            </div>
            <h3 className={shared.cardTitle}>Todo tu stock en orden y visible</h3>
            <MutedText>
              Olvídate de buscar en cuadernos viejos o adivinar qué hay en bodega. Encuentra
              existencias, ubicaciones y cantidades exactas en segundos.
            </MutedText>
          </article>

          <article className={`${shared.card} ${styles.card}`}>
            <div className={styles.iconWrapper}>
              <EventRoundedIcon />
            </div>
            <h3 className={shared.cardTitle}>Alertas inteligentes de vencimiento</h3>
            <MutedText>
              El sistema te avisa con tiempo qué lotes están por expirar para que los muevas antes
              de que se conviertan en dinero perdido.
            </MutedText>
          </article>

          <article className={`${shared.card} ${styles.card}`}>
            <div className={styles.iconWrapper}>
              <QrCodeScannerRoundedIcon />
            </div>
            <h3 className={shared.cardTitle}>Lectura rápida con código de barras</h3>
            <MutedText>
              Pasa el lector y el producto aparece al instante. Si un artículo no tiene código,
              Stockma te ayuda a crearlo y asignarlo sin enredos.
            </MutedText>
          </article>

          <article className={`${shared.card} ${styles.card}`}>
            <div className={styles.iconWrapper}>
              <SyncAltRoundedIcon />
            </div>
            <h3 className={shared.cardTitle}>Tu equipo conectado en tiempo real</h3>
            <MutedText>
              Ventas, reabastecimiento y ajustes sincronizados al mismo tiempo. Todos trabajan sobre
              la misma información y sin pisarse el trabajo.
            </MutedText>
          </article>

          <article className={`${shared.card} ${styles.card}`}>
            <div className={styles.iconWrapper}>
              <CreditCardRoundedIcon />
            </div>
            <h3 className={shared.cardTitle}>Planes flexibles que crecen contigo</h3>
            <MutedText>
              Empieza a probar la plataforma sin riesgo con 8 días gratis. Elige el plan que mejor
              se adapte a tu local y cámbialo cuando lo necesites.
            </MutedText>
          </article>

          <article className={`${shared.card} ${styles.card}`}>
            <div className={styles.iconWrapper}>
              <FileDownloadRoundedIcon />
            </div>
            <h3 className={shared.cardTitle}>Reportes claros para tu tranquilidad</h3>
            <MutedText>
              Obtén información limpia y directa sobre qué se vende más, qué se queda quieto y saca
              los datos que tu contador te pide sin sufrir.
            </MutedText>
            <span className={styles.badge}>Próximamente</span>
          </article>
        </div>
      </div>
    </section>
  )
}
