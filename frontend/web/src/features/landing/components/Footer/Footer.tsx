import logoDarkUrl from '../../../../assets/Stockma_vertical_trim_dark.png'
import logoUrl from '../../../../assets/Stockma_vertical_trim.png'
import { MutedText } from '../../../../styles/components/texts'
import styles from './Footer.module.css'
import { SupportText } from './Footer.styles'

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
              <svg viewBox='0 0 24 24' aria-hidden='true'>
                <path d='M18.9 5.11A9.65 9.65 0 0 0 12.02 2C6.61 2 2.19 6.4 2.19 11.81c0 1.72.45 3.39 1.3 4.87L2 22l5.5-1.46a9.82 9.82 0 0 0 4.52 1.09h.01c5.41 0 9.83-4.4 9.83-9.82 0-2.63-1.03-5.11-2.87-6.7ZM12.03 19.3a8.13 8.13 0 0 1-4.14-1.12l-.3-.18-3.27.87.87-3.18-.2-.33A8.13 8.13 0 0 1 3.8 11.8c0-4.49 3.66-8.15 8.15-8.15a8.14 8.14 0 0 1 8.15 8.15c0 4.49-3.66 8.15-8.15 8.15Zm4.46-6.08c-.24-.12-1.43-.7-1.65-.78s-.38-.12-.54.12-.62.78-.76.94-.28.18-.52.06c-1.42-.7-2.35-2.5-2.56-2.93-.21-.43-.02-.66.12-.88.12-.12.24-.3.36-.45.12-.15.16-.27.24-.45.08-.18.04-.34-.02-.46-.06-.12-.54-1.3-.74-1.79-.19-.47-.38-.4-.54-.4h-.46c-.15 0-.4.06-.61.3-.2.24-.77.75-.77 1.83s.79 2.13.9 2.28c.11.15 1.54 2.34 3.73 3.28.52.22.93.35 1.25.45.52.17.99.15 1.36.09.41-.06 1.43-.58 1.62-1.14.2-.56.2-1.04.14-1.14-.06-.11-.22-.18-.46-.3Z' />
              </svg>
            </a>
            <a
              className={styles.socialLink}
              href='https://www.instagram.com/stockma_app?dlrf=MXAzNTU1OWl5YzIwZw=='
              target='_blank'
              rel='noreferrer'
              aria-label='Instagram de Stockma'
            >
              <svg viewBox='0 0 24 24' aria-hidden='true'>
                <path d='M7 2h10a5 5 0 0 1 5 5v10a5 5 0 0 1-5 5H7a5 5 0 0 1-5-5V7a5 5 0 0 1 5-5Zm0 2a3 3 0 0 0-3 3v10a3 3 0 0 0 3 3h10a3 3 0 0 0 3-3V7a3 3 0 0 0-3-3H7Zm5 3.5A4.5 4.5 0 1 1 7.5 12 4.5 4.5 0 0 1 12 7.5Zm0 2A2.5 2.5 0 1 0 14.5 12 2.5 2.5 0 0 0 12 9.5Zm5-3.25a1.25 1.25 0 1 1-1.25 1.25A1.25 1.25 0 0 1 17 6.25Z' />
              </svg>
            </a>
          </div>
        </div>
        <SupportText>
          Atención, preguntas frecuentes y soporte:{' '}
          <a href='https://w.app/stockmaapp' target='_blank' rel='noreferrer'>
            Escribenos al WhatsApp
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
