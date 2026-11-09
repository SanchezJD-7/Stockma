import logoDarkUrl from '../../../../assets/Stockma_vertical_trim_dark.png'
import logoUrl from '../../../../assets/Stockma_vertical_trim.png'
import { MutedText } from '../../../../styles/components/texts'
import styles from './Footer.module.css'
import { SupportText } from './Footer.styles'
import WhatsAppIcon from '@mui/icons-material/WhatsApp'
import InstagramIcon from '@mui/icons-material/Instagram'

export function Footer() {
  return (
    <footer id='landing-footer' className={styles.root}>
      <div className={styles.inner}>
        <div className={styles.top}>
          <img className={`${styles.logo} ${styles.logoLight}`} src={logoUrl} alt='Stockma' />
          <img className={`${styles.logo} ${styles.logoDark}`} src={logoDarkUrl} alt='Stockma' />
          <div className={styles.social}>
            <a
              className={styles.socialLink}
              href='https://w.app/stockmaapp'
              target='_blank'
              rel='noreferrer'
              aria-label='WhatsApp de Stockma'
            >
              <WhatsAppIcon fontSize='small' />
            </a>
            <a
              className={styles.socialLink}
              href='https://www.instagram.com/stockma_app?dlrf=MXAzNTU1OWl5YzIwZw=='
              target='_blank'
              rel='noreferrer'
              aria-label='Instagram de Stockma'
            >
              <InstagramIcon fontSize='small' />
            </a>
          </div>
        </div>
        <SupportText>
          Atención, preguntas frecuentes y soporte:{' '}
          <a href='https://w.app/stockmaapp' target='_blank' rel='noreferrer'>
            Escríbenos al WhatsApp
          </a>
        </SupportText>
        <div className={styles.bottom}>
          <MutedText>© 2026 Stockma</MutedText>
          <MutedText>Hecho en Colombia</MutedText>
        </div>
      </div>
    </footer>
  )
}
